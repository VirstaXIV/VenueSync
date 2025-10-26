using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using OtterGui.Classes;
using VenueSync.Events;
using VenueSync.Services;
using VenueSync.Services.Api.Venue;
using VenueSync.State;

namespace VenueSync.Ui.Crud.Venue;

public class ManageStreamWindow : Window, IDisposable
{
    private readonly StateService _stateService;
    private readonly StreamApi _streamApi;
    private readonly ITextureProvider _textureProvider;
    private readonly SyncFileService _syncFileService;
    private readonly FileDialogManager _fileDialog = new();

    private bool _isEditMode;
    private string _venueId = string.Empty;
    private string _streamId = string.Empty;
    private string _name = string.Empty;
    private string _username = string.Empty;
    private string _type = string.Empty;
    private bool _isSubmitting;

    private static readonly string[] AllowedTypes = ["twitch", "kick", "youtube"];

    // Logo upload state
    private string _selectedLogoPath = string.Empty;
    private string _currentLogoFile = string.Empty;
    private string _currentLogoHash = string.Empty;
    private IDalamudTextureWrap? _logoPreviewTexture;
    private bool _isUploadingLogo;

    public ManageStreamWindow(
        StateService stateService,
        StreamApi streamApi,
        ITextureProvider textureProvider,
        SyncFileService syncFileService,
        ServiceDisconnected serviceDisconnected) : base("Manage Stream")
    {
        _stateService = stateService;
        _streamApi = streamApi;
        _textureProvider = textureProvider;
        _syncFileService = syncFileService;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 260),
            MaximumSize = new Vector2(900, 600),
        };

        RespectCloseHotkey = true;
        IsOpen = false;

        serviceDisconnected.Subscribe(OnDisconnect, ServiceDisconnected.Priority.High);
    }

    private void OnDisconnect()
    {
        if (IsOpen)
        {
            Toggle();
        }
    }

    public void OpenForCreate(string venueId)
    {
        _isEditMode = false;
        _venueId = venueId;
        _streamId = string.Empty;
        _name = string.Empty;
        _username = string.Empty;
        _type = "twitch";
        _selectedLogoPath = string.Empty;
        _currentLogoFile = string.Empty;
        _currentLogoHash = string.Empty;
        DisposeLogoPreview();
        _isUploadingLogo = false;
        IsOpen = true;
    }

    public void OpenForEdit(string venueId, UserVenueStreamItem stream)
    {
        _isEditMode = true;
        _venueId = venueId;
        _streamId = stream.id;
        _name = stream.name;
        _username = stream.username;
        _type = SanitizeType(stream.type);
        _selectedLogoPath = string.Empty;
        _currentLogoFile = stream.logo;
        _currentLogoHash = stream.hash;
        _isUploadingLogo = false;

        _ = TryLoadExistingLogoAsync(_currentLogoFile, _currentLogoHash);

        IsOpen = true;
    }

    public override void PreDraw()
    {
        WindowName = _isEditMode ? "Edit Stream###ManageStreamWindow" : "Create Stream###ManageStreamWindow";
    }

    public override void Draw()
    {
        ImGui.TextUnformatted(_isEditMode ? "Edit stream" : "Create a new stream");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Name");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var nameBuf = _name;
        if (ImGui.InputText("##StreamName", ref nameBuf, 128))
            _name = nameBuf;

        ImGui.TextUnformatted("Username");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var userBuf = _username;
        if (ImGui.InputText("##StreamUsername", ref userBuf, 128))
            _username = userBuf;

        ImGui.TextUnformatted("Type");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var allowed = AllowedTypes;
        var currentIndex = Math.Max(0, Array.IndexOf(allowed, SanitizeType(_type)));
        if (ImGui.Combo("##StreamType", ref currentIndex, allowed, allowed.Length))
            _type = allowed[currentIndex];

        if (_isEditMode)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextUnformatted("Logo");
            if (!string.IsNullOrEmpty(_selectedLogoPath))
            {
                ImGui.TextUnformatted($"Selected: {_selectedLogoPath}");
            }

            var disableSelect = _isUploadingLogo;
            if (disableSelect) ImGui.BeginDisabled();
            if (ImGui.Button("Select Logo..."))
            {
                _fileDialog.OpenFileDialog(
                    "Select Stream Logo",
                    "Image files{.png,.jpg,.jpeg,.gif},.*",
                    (ok, files) =>
                    {
                        if (ok && files is { Count: > 0 })
                        {
                            _selectedLogoPath = files[0];
                            _ = Task.Run(async () => await LoadPreviewFromPathAsync(_selectedLogoPath).ConfigureAwait(false));
                        }
                    },
                    1);
            }
            if (disableSelect) ImGui.EndDisabled();

            ImGui.SameLine();
            var canUpload = !_isUploadingLogo && !string.IsNullOrWhiteSpace(_venueId) && !string.IsNullOrWhiteSpace(_streamId) && File.Exists(_selectedLogoPath);
            if (!canUpload) ImGui.BeginDisabled();
            if (ImGui.Button("Upload Logo"))
            {
                _isUploadingLogo = true;
                _ = UploadLogoAsync();
            }
            if (!canUpload) ImGui.EndDisabled();

            ImGui.Spacing();
            if (_logoPreviewTexture != null)
            {
                ImGui.Image(_logoPreviewTexture.Handle, new Vector2(200, 200));
                ImGui.Spacing();
            }

            _fileDialog.Draw();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var canSubmit = !_isSubmitting && !string.IsNullOrWhiteSpace(_venueId) && (_isEditMode || !string.IsNullOrWhiteSpace(_name));
        if (!canSubmit) ImGui.BeginDisabled();
        if (ImGui.Button(_isEditMode ? "Update Stream" : "Create Stream"))
        {
            _isSubmitting = true;
            _ = SubmitAsync();
            IsOpen = false;
        }
        if (!canSubmit) ImGui.EndDisabled();
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }

    private static string SanitizeType(string? type)
    {
        var t = type?.Trim().ToLowerInvariant() ?? string.Empty;
        return AllowedTypes.Contains(t) ? t : "twitch";
    }

    private void DisposeLogoPreview()
    {
        try
        {
            _logoPreviewTexture?.Dispose();
        }
        catch
        {
            // ignored
        }
        finally
        {
            _logoPreviewTexture = null;
        }
    }

    private async Task LoadPreviewFromPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            await using var fs = File.OpenRead(path);
            var tex = await _textureProvider.CreateFromImageAsync(fs, leaveOpen: false, cancellationToken: CancellationToken.None).ConfigureAwait(false);
            var old = _logoPreviewTexture;
            _logoPreviewTexture = tex;
            old?.Dispose();
        }
        catch (Exception ex)
        {
            VenueSync.Log.Warning($"Failed to load stream logo preview: {ex.Message}");
        }
    }

    private Task TryLoadExistingLogoAsync(string file, string hash)
    {
        if (string.IsNullOrWhiteSpace(_streamId) || string.IsNullOrWhiteSpace(file))
            return Task.CompletedTask;

        _syncFileService.MaybeDownloadFile(
            _streamId,
            file,
            "png",
            hash,
            path => _ = Task.Run(async () =>
            {
                if (path == null) return;
                await LoadPreviewFromPathAsync(path).ConfigureAwait(false);
            }));

        return Task.CompletedTask;
    }

    private async Task UploadLogoAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_venueId) || string.IsNullOrWhiteSpace(_streamId) || !File.Exists(_selectedLogoPath))
            {
                VenueSync.Messager.NotificationMessage("No logo selected", NotificationType.Error);
                return;
            }

            await using var stream = File.OpenRead(_selectedLogoPath);
            var fileName = Path.GetFileName(_selectedLogoPath);
            var contentType = GetContentType(_selectedLogoPath);

            var result = await _streamApi.UploadLogoAsync(_venueId, _streamId, stream, fileName, contentType).ConfigureAwait(false);
            if (result is { Success: true, Data: not null })
            {
                var updatedStream = result.Data;
                var venues = _stateService.UserState.venues;
                var venue = venues.FirstOrDefault(v => v.id == _venueId);
                if (venue is not null)
                {
                    var list = venue.streams;
                    var idx = list.FindIndex(s => s.id == updatedStream.id);
                    if (idx >= 0)
                        list[idx] = updatedStream;
                    else
                        list.Add(updatedStream);
                }

                _currentLogoFile = updatedStream.logo;
                _currentLogoHash = updatedStream.hash;
                _selectedLogoPath = string.Empty;

                VenueSync.Messager.NotificationMessage("Logo uploaded successfully", NotificationType.Success);
                await TryLoadExistingLogoAsync(_currentLogoFile, _currentLogoHash);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Logo upload failed", NotificationType.Error);
                VenueSync.Log.Warning($"Stream logo upload failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Logo upload failed", NotificationType.Error);
            VenueSync.Log.Warning($"Stream logo upload exception: {ex.Message}");
        }
        finally
        {
            _isUploadingLogo = false;
        }
    }

    private async Task SubmitAsync()
    {
        var t = SanitizeType(_type);
        var payload = new System.Collections.Generic.Dictionary<string, object?>()
        {
            ["name"] = _name,
            ["username"] = _username,
            ["type"] = t,
        };

        try
        {
            ApiResult<UserVenueStreamItem> result;
            if (_isEditMode)
            {
                var streamId = _streamId;
                result = await _streamApi.UpdateAsync(_venueId, streamId, payload).ConfigureAwait(false);
            }
            else
            {
                result = await _streamApi.StoreAsync(_venueId, payload).ConfigureAwait(false);
            }

            if (result is { Success: true, Data: not null })
            {
                var streamItem = result.Data;
                var venues = _stateService.UserState.venues;
                var venue = venues.FirstOrDefault(v => v.id == _venueId);
                if (venue is not null)
                {
                    var list = venue.streams;
                    var idx = list.FindIndex(s => s.id == streamItem.id);
                    if (idx >= 0)
                        list[idx] = streamItem;
                    else
                        list.Add(streamItem);
                }

                _streamId = streamItem.id;

                VenueSync.Messager.NotificationMessage(_isEditMode ? "Stream updated successfully" : "Stream created successfully", NotificationType.Success);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Stream save failed", NotificationType.Error);
                VenueSync.Log.Warning($"Stream submit failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Stream save failed", NotificationType.Error);
            VenueSync.Log.Warning($"Stream submit exception: {ex.Message}");
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    public void Dispose()
    {
        if (IsOpen)
        {
            Toggle();
        }
        DisposeLogoPreview();
    }
}
