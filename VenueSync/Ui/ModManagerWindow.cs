using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiNotification;
using OtterGui.Classes;
using VenueSync.Events;
using VenueSync.Services;
using VenueSync.Services.Api;
using VenueSync.State;

namespace VenueSync.Ui;

public class ModManagerWindow : Window
{
    private readonly ModsState _modsState;
    private readonly ModsApi _modsApi;
    private readonly PreviewModService _previewService;
    private readonly StateService _stateService;
    private readonly IPCManager _ipcManager;
    private readonly AccountApi _accountApi;
    private readonly Configuration _configuration;

    private int _selectedIndex = -1;
    private bool _confirmDelete;
    private bool _isSaving;
    private bool _isUploading;
    private bool _dirty;

    private bool _dialogIsOpen;
    private readonly FileDialogManager? _fileDialogManager;

    private string _editName = "";
    private bool _editPublic = false;
    private string _editActiveVersionId = "";
    private string _penumbraFolder = "";

    private bool _confirmDeleteVersion;
    private string _pendingDeleteVersionId = "";
    private string _pendingDeleteVersionName = "";
    private string _pendingDeleteModId = "";

    public ModManagerWindow(
        ModsState modsState,
        ModsApi modsApi,
        PreviewModService previewService,
        StateService stateService,
        FileDialogManager fileDialogManager,
        IPCManager ipcManager,
        AccountApi accountApi,
        Configuration configuration,
        ServiceDisconnected serviceDisconnected
    ) : base("Mod Management###VenueSyncModManager")
    {
        _modsState = modsState;
        _modsApi = modsApi;
        _previewService = previewService;
        _stateService = stateService;
        _fileDialogManager = fileDialogManager;
        _ipcManager = ipcManager;
        _accountApi = accountApi;
        _configuration = configuration;

        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = new Vector2(800, 500),
            MaximumSize = new Vector2(4000, 4000)
        };

        serviceDisconnected.Subscribe(OnDisconnect, ServiceDisconnected.Priority.High);
    }

    private void OnDisconnect()
    {
        if (IsOpen)
        {
            Toggle();
        }
    }

    public override void OnOpen()
    {
        try
        {
            if (_configuration.PenumbraLinks != null && _configuration.PenumbraLinks.Count > 0)
            {
                _modsState.penumbraLinks = _configuration.PenumbraLinks
                    .Select(l => new ModPenumbraLink { mod_id = l.mod_id, path = l.path })
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            VenueSync.Log.Warning($"Failed to load penumbra links from configuration: {ex.Message}");
        }
    }

    public override void Draw()
    {
        var avail = ImGui.GetContentRegionAvail();
        var leftWidth = MathF.Max(240f, avail.X * 0.33f);

        if (_modsState.modList.Count == 0 && _stateService.ModsState.modList.Count > 0)
        {
            VenueSync.Log.Debug($"ModManagerWindow syncing mods list from global state ({_stateService.ModsState.modList.Count} items).");
            _modsState.modList = _stateService.ModsState.modList;
        }

        ImGui.BeginChild("##mods_left", new Vector2(leftWidth, 0), true);
        ImGui.Text("Mods");
        ImGui.SameLine();
        if (ImGui.SmallButton("+ New"))
        {
            var newMod = new ModItem {
                id = "",
                name = "New Mod",
                version_id = "",
                is_public = false,
                versions = []
            };
            _modsState.modList.Add(newMod);
            _selectedIndex = _modsState.modList.Count - 1;
            LoadEditorFromModel();
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(_selectedIndex < 0);
        if (ImGui.SmallButton("Delete"))
        {
            _confirmDelete = true;
        }
        ImGui.EndDisabled();

        if (_confirmDelete)
        {
            ImGui.OpenPopup("Confirm Delete");
            _confirmDelete = false;
        }

        ImGui.SetNextWindowSizeConstraints(new Vector2(420, 180), new Vector2(600, 600));
        ImGui.SetNextWindowSize(new Vector2(480, 200), ImGuiCond.Appearing);
        if (ImGui.BeginPopupModal("Confirm Delete"))
        {
            ImGui.TextWrapped("Are you sure you want to delete this mod? This cannot be undone.");
            if (ImGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Delete"))
            {
                DeleteSelectedMod();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        ImGui.Separator();

        for (var i = 0; i < _modsState.modList.Count; i++)
        {
            var mod = _modsState.modList[i];
            var selected = i == _selectedIndex;
            if (ImGui.Selectable($"{(string.IsNullOrWhiteSpace(mod.name) ? "(unnamed)" : mod.name)}##mod_{i}", selected))
            {
                _selectedIndex = i;
                LoadEditorFromModel();
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##mods_right", new Vector2(0, 0), true);
        if (_selectedIndex >= 0 && _selectedIndex < _modsState.modList.Count)
        {
            var mod = _modsState.modList[_selectedIndex];
            ImGui.Text("Edit Mod");
            ImGui.Separator();

            DrawTopFields(mod);

            ImGui.Spacing();
            ImGui.Separator();

            DrawPenumbraFolderSelection(mod);

            ImGui.Spacing();
            ImGui.Separator();

            if (!string.IsNullOrWhiteSpace(mod.id))
            {
                ImGui.Text("Versions");
                ImGui.Spacing();
                DrawVersionsToolbar(mod);
                ImGui.Spacing();
                DrawVersionsTable(mod);
            }
            else
            {
                ImGui.TextDisabled("Save to service to begin uploading versions.");
            }
        }
        else
        {
            ImGui.TextDisabled("Select a mod from the left or create a new one.");
        }
        ImGui.EndChild();
    }

    private void DrawTopFields(ModItem mod)
    {
        var idStr = mod.id;
        ImGui.InputText("Id", ref idStr, 256, ImGuiInputTextFlags.ReadOnly);

        if (ImGui.InputText("Name", ref _editName, 256))
            _dirty = true;

        if (ImGui.Checkbox("Public", ref _editPublic))
            _dirty = true;

        if (mod.versions.Count > 0)
        {
            var currentIndex = Math.Max(0, mod.versions.FindIndex(v => v.id == _editActiveVersionId));
            var items = mod.versions.Select(v => v.name).ToArray();
            if (ImGui.Combo("Active Version", ref currentIndex, items, items.Length))
            {
                _editActiveVersionId = mod.versions[currentIndex].id;
                _dirty = true;
            }
        }
        else
        {
            ImGui.TextDisabled("No versions available.");
        }

        ImGui.Spacing();

        var missingActiveVersionButHasVersions = mod.versions.Count > 0 && string.IsNullOrWhiteSpace(mod.version_id);
        var canSave = !_isSaving && !string.IsNullOrWhiteSpace(_editName) && (_dirty || missingActiveVersionButHasVersions);
        ImGui.BeginDisabled(!canSave);
        {
            var label = _isSaving ? "Saving..." : "Save to Service";
            if (ImGui.Button(label, new Vector2(160, 0)))
            {
                PushEditorToModel();
                _ = SaveCurrentAsync();
            }
        }
        ImGui.EndDisabled();
    }

    private void DrawPenumbraFolderSelection(ModItem mod)
    {
        ImGui.Text("Penumbra Folder Selection (local-only)");
        ImGui.Spacing();

        var stored = _modsState.penumbraLinks.FirstOrDefault(p => p.mod_id == mod.id);
        if (stored != null && string.IsNullOrWhiteSpace(_penumbraFolder))
        {
            _penumbraFolder = stored.path;
        }

        if (ImGui.InputText("Folder", ref _penumbraFolder))
        {
            UpsertPenumbraLink(mod.id, _penumbraFolder);
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(_fileDialogManager == null);
        if (ImGui.Button("Browse..."))
        {
            OpenPenumbraFolderDialog(mod.id);
        }
        ImGui.EndDisabled();

        if (_dialogIsOpen)
        {
            _fileDialogManager?.Draw();
        }

        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(_penumbraFolder));
        if (ImGui.Button("Preview"))
        {
            if (_previewService.ValidateFolder(_penumbraFolder, out var fileList, out var error))
            {
                VenueSync.Log.Debug("default_mod was valid, previewing...");
                _stateService.VenueState.mods_preview_active = true;
                if (!_previewService.Preview(fileList))
                {
                    VenueSync.Log.Warning("Failed to preview selected folder.");
                }
            }
            else
            {
                VenueSync.Log.Warning(error ?? "Invalid Penumbra folder.");
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear Preview"))
        {
            _previewService.ClearPreview();
            _stateService.VenueState.mods_preview_active = false;
        }
        ImGui.EndDisabled();
    }

    private void DrawVersionsToolbar(ModItem mod)
    {
        ImGui.BeginDisabled(_isUploading || _fileDialogManager == null);
        var defaultFolder = _ipcManager.Penumbra.ExportDirectory ?? @"C:\";
        if (ImGui.Button(_isUploading ? "Uploading..." : "Upload Version (.pmp)"))
        {
            _dialogIsOpen = true;
            _fileDialogManager?.OpenFileDialog(
                "Select Mod Package",
                "Penumbra Package{.pmp},.*",
                (ok, files) =>
                {
                    _dialogIsOpen = false;
                    if (!ok || files is not { Count: > 0 })
                        return;

                    _ = UploadVersionAsync(mod, files[0]);
                },
                1, defaultFolder);
        }
        ImGui.EndDisabled();
    }

    private void DrawVersionsTable(ModItem mod)
    {
        if (ImGui.BeginTable("VersionsTable", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Version", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 110.0f);
            ImGui.TableHeadersRow();

            foreach (var v in mod.versions.ToList())
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.Text($"{v.name}");

                ImGui.TableSetColumnIndex(1);
                ImGui.BeginDisabled(_modsApi == null);

                var style = ImGui.GetStyle();
                var btnText = "Delete";
                var btnWidth = ImGui.CalcTextSize(btnText).X + style.FramePadding.X * 2;
                var availX = ImGui.GetContentRegionAvail().X;
                if (availX > btnWidth)
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (availX - btnWidth));

                if (ImGui.SmallButton($"{btnText}##ver_{v.id}"))
                {
                    _pendingDeleteVersionId = v.id;
                    _pendingDeleteVersionName = v.name ?? v.id;
                    _pendingDeleteModId = mod.id;
                    _confirmDeleteVersion = true;
                }
                ImGui.EndDisabled();
            }

            ImGui.EndTable();
        }

        if (_confirmDeleteVersion)
        {
            ImGui.OpenPopup("Confirm Delete Version");
            _confirmDeleteVersion = false;
        }

        ImGui.SetNextWindowSizeConstraints(new Vector2(420, 180), new Vector2(600, 600));
        ImGui.SetNextWindowSize(new Vector2(480, 200), ImGuiCond.Appearing);
        if (ImGui.BeginPopupModal("Confirm Delete Version"))
        {
            ImGui.TextWrapped($"Are you sure you want to delete version '{_pendingDeleteVersionName}'? This cannot be undone.");
            if (ImGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Delete"))
            {
                var m = _modsState.modList.FirstOrDefault(m => m.id == _pendingDeleteModId);
                if (m != null && !string.IsNullOrWhiteSpace(_pendingDeleteVersionId))
                {
                    _ = DeleteVersionAsync(m, _pendingDeleteVersionId);
                }
                ImGui.CloseCurrentPopup();
                _pendingDeleteVersionId = "";
                _pendingDeleteVersionName = "";
                _pendingDeleteModId = "";
            }
            ImGui.EndPopup();
        }
    }

    private void UpsertPenumbraLink(string modId, string folder)
    {
        var existing = _modsState.penumbraLinks.FirstOrDefault(p => p.mod_id == modId);
        if (existing == null)
        {
            var link = new ModPenumbraLink
            {
                mod_id = modId,
                path = folder
            };
            _modsState.penumbraLinks.Add(link);
        }
        else
        {
            existing.path = folder;
        }

        var existingCfg = _configuration.PenumbraLinks.FirstOrDefault(p => p.mod_id == modId);
        if (existingCfg == null)
        {
            _configuration.PenumbraLinks.Add(new ModPenumbraLink
            {
                mod_id = modId,
                path = folder
            });
        }
        else
        {
            existingCfg.path = folder;
        }

        _configuration.Save();
    }

    private void LoadEditorFromModel()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _modsState.modList.Count) return;

        var mod = _modsState.modList[_selectedIndex];
        _editName = mod.name;
        _editPublic = mod.is_public;
        _editActiveVersionId = mod.version_id;
        _penumbraFolder = _modsState.penumbraLinks.FirstOrDefault(p => p.mod_id == mod.id)?.path ?? "";
        _dirty = false;
    }

    private void PushEditorToModel()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _modsState.modList.Count) return;
        var mod = _modsState.modList[_selectedIndex];
        mod.name = _editName;
        mod.is_public = _editPublic;
        mod.version_id = _editActiveVersionId;
    }

    private async Task SaveCurrentAsync()
    {
        try
        {
            _isSaving = true;
            var mod = _modsState.modList[_selectedIndex];
            var wasNew = string.IsNullOrWhiteSpace(mod.id);
            var result = await _modsApi.SaveMod(mod).ConfigureAwait(false);
            if (result is { Success: true, Data: not null })
            {
                var updated = result.Data;

                if (wasNew)
                {
                    mod.id = updated.id;
                    mod.name = updated.name;
                    mod.is_public = updated.is_public;
                    mod.version_id = updated.version_id;
                    mod.versions = updated.versions;

                    _editName = updated.name;
                    _editPublic = updated.is_public;
                    _editActiveVersionId = updated.version_id;
                }
                else
                {
                    var idx = _modsState.modList.FindIndex(m => m.id == updated.id);
                    if (idx >= 0)
                        _modsState.modList[idx] = updated;
                    else
                        _modsState.modList.Add(updated);
                    _selectedIndex = _modsState.modList.FindIndex(m => m.id == updated.id);
                }

                LoadEditorFromModel();
                _dirty = false;
                VenueSync.Log.Information($"Saved mod '{(wasNew ? mod.name : updated.name)}'");
            }
            else
            {
                VenueSync.Log.Warning($"Failed to save mod: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task UploadVersionAsync(ModItem mod, string filePath)
    {
        try
        {
            _isUploading = true;
            var result = await _modsApi.UploadVersion(mod.id, filePath).ConfigureAwait(false);
            if (result is { Success: true, Data: not null })
            {
                var updated = result.Data;
                var idx = _modsState.modList.FindIndex(m => m.id == updated.id);
                if (idx >= 0)
                    _modsState.modList[idx] = updated;
                else
                    _modsState.modList.Add(updated);

                _selectedIndex = _modsState.modList.FindIndex(m => m.id == updated.id);
                _editActiveVersionId = updated.version_id;
                LoadEditorFromModel();

                VenueSync.Log.Information("Uploaded version successfully.");
            }
            else
            {
                VenueSync.Log.Warning($"Version upload failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        finally
        {
            _isUploading = false;
        }
    }

    private async Task DeleteVersionAsync(ModItem mod, string versionId)
    {
        var result = await _modsApi.DeleteVersion(mod.id, versionId).ConfigureAwait(false);
        if (result is { Success: true, Data: not null })
        {
            var updated = result.Data;
            var idx = _modsState.modList.FindIndex(m => m.id == updated.id);
            if (idx >= 0)
                _modsState.modList[idx] = updated;
            else
                _modsState.modList.Add(updated);

            _selectedIndex = _modsState.modList.FindIndex(m => m.id == updated.id);
            _editActiveVersionId = updated.version_id;
            LoadEditorFromModel();
        }
        else
        {
            VenueSync.Log.Warning($"Failed to delete version: {result.ErrorMessage ?? "Unknown error"}");
        }
    }

    private void DeleteSelectedMod()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _modsState.modList.Count) return;

        var mod = _modsState.modList[_selectedIndex];

        async Task DoDeleteAsync()
        {
            var ok = true;
            if (!string.IsNullOrWhiteSpace(mod.id))
            {
                var result = await _modsApi.DeleteMod(mod.id).ConfigureAwait(false);
                ok = result.Success;
            }

            if (ok)
            {
                // Remove any related penumbra link(s) from local state and configuration and persist
                _modsState.penumbraLinks.RemoveAll(p => p.mod_id == mod.id);
                _configuration.PenumbraLinks.RemoveAll(p => p.mod_id == mod.id);
                _configuration.Save();

                var user = await _accountApi.User().ConfigureAwait(false);
                if (user.Success)
                {
                    _modsState.modList = _stateService.ModsState.modList;
                    LoadEditorFromModel();

                    VenueSync.Messager.NotificationMessage("Mod deleted successfully", NotificationType.Success);
                }
                else
                {
                    VenueSync.Messager.NotificationMessage("Mod deleted, but failed to refresh user data", NotificationType.Warning);
                }
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Failed to delete mod from service", NotificationType.Error);
                VenueSync.Log.Warning("Failed to delete mod from service.");
            }
        }

        _ = DoDeleteAsync();
    }

    private void OpenPenumbraFolderDialog(string modId)
    {
        _dialogIsOpen = true;
        var defaultFolder = _ipcManager.Penumbra.ModDirectory ?? @"C:\";
        _fileDialogManager?.OpenFolderDialog("Select Penumbra Mod Folder", (success, path) =>
        {
            _dialogIsOpen = false;
            if (!success) return;

            _penumbraFolder = path;
            UpsertPenumbraLink(modId, _penumbraFolder);
        }, string.IsNullOrWhiteSpace(_penumbraFolder) ? defaultFolder : _penumbraFolder);
    }

}
