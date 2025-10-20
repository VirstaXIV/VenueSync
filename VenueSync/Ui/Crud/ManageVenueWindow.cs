using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using OtterGui.Classes;
using VenueSync.Events;
using VenueSync.Services;
using VenueSync.Services.Api;
using VenueSync.Services.Api.Venue;
using VenueSync.State;
using VenueSync.Ui.Crud.Venue;
using LocationApi = VenueSync.Services.Api.Venue.LocationApi;

namespace VenueSync.Ui.Crud;

public class ManageVenueWindow : Window, IDisposable
{
    private readonly StateService _stateService;
    private readonly VenueApi _venueApi;
    private readonly FileDialogManager _fileDialog;
    private readonly ITextureProvider _textureProvider;
    private readonly SyncFileService _syncFileService;
    private readonly ManageStaffWindow _manageStaffWindow;
    private readonly ManageStreamWindow _manageStreamWindow;
    private readonly ManageScheduleWindow _manageScheduleWindow;
    private readonly ManageLocationWindow _manageLocationWindow;
    private readonly StaffApi _staffApi;
    private readonly StreamApi _streamApi;
    private readonly ScheduleApi _scheduleApi;
    private readonly LocationApi _locationApi;
    private readonly AccountApi _accountApi;

    private bool _isEditMode;
    private string _venueId = string.Empty;

    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _discordInvite = string.Empty;
    private string _carrdUrl = string.Empty;
    private string _tagsCsv = string.Empty;

    private string _selectedLogoPath = string.Empty;
    private string _currentLogoFileId = string.Empty;
    private string _currentLogoHash = string.Empty;
    private IDalamudTextureWrap? _logoPreviewTexture;
    private bool _isSubmitting;
    private bool _isUploadingLogo;

    public ManageVenueWindow(
        StateService stateService, 
        VenueApi venueApi, 
        FileDialogManager fileDialog, 
        ITextureProvider textureProvider, 
        SyncFileService syncFileService, 
        ManageStaffWindow manageStaffWindow, 
        ManageStreamWindow manageStreamWindow, 
        ManageScheduleWindow manageScheduleWindow, 
        ManageLocationWindow manageLocationWindow,
        StaffApi staffApi, 
        StreamApi streamApi,
        ScheduleApi scheduleApi,
        LocationApi locationApi,
        AccountApi accountApi,
        ServiceDisconnected serviceDisconnected
        ) : base("Manage Venue")
    {
        _stateService = stateService;
        _venueApi = venueApi;
        _fileDialog = fileDialog;
        _textureProvider = textureProvider;
        _syncFileService = syncFileService;
        _manageStaffWindow = manageStaffWindow;
        _manageStreamWindow = manageStreamWindow;
        _manageScheduleWindow = manageScheduleWindow;
        _manageLocationWindow = manageLocationWindow;
        _staffApi = staffApi;
        _streamApi = streamApi;
        _scheduleApi = scheduleApi;
        _locationApi = locationApi;
        _accountApi = accountApi;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 420),
            MaximumSize = new Vector2(1200, 900),
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

    public void OpenForCreate()
    {
        _isEditMode = false;
        _venueId = string.Empty;

        _name = string.Empty;
        _description = string.Empty;
        _discordInvite = string.Empty;
        _carrdUrl = string.Empty;
        _tagsCsv = string.Empty;
        _selectedLogoPath = string.Empty;
        _currentLogoFileId = string.Empty;
        _currentLogoHash = string.Empty;
        DisposeLogoPreview();

        IsOpen = true;
    }

    public void OpenForEdit(UserVenueItem venue)
    {
        _isEditMode = true;
        _venueId = venue.id;

        _name = venue.name;
        _discordInvite = venue.discord_invite;
        _description = venue.description;
        _carrdUrl = venue.carrd_url;
        _tagsCsv = string.Join(", ", venue.tags);
        _currentLogoFileId = venue.logo;
        _currentLogoHash = venue.hash;

        _ = TryLoadExistingLogoAsync(_currentLogoFileId, _currentLogoHash);

        IsOpen = true;
    }

    public override void PreDraw()
    {
        WindowName = _isEditMode ? "Edit Venue###ManageVenueWindow" : "Create Venue###ManageVenueWindow";
    }

    public override void Draw()
    {
        ImGui.TextUnformatted(_isEditMode ? "Edit venue" : "Create a new venue");
        ImGui.Separator();
        ImGui.Spacing();

        if (_isEditMode)
        {
            DrawEditTabs();
        }
        else
        {
            DrawGeneralSection();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawEditTabs()
    {
        if (ImGui.BeginTabBar("ManageVenueTabs"))
        {
            if (ImGui.BeginTabItem("Overview"))
            {
                DrawGeneralSection();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Logo"))
            {
                DrawLogoSection();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Locations"))
            {
                DrawLocationsTable();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Staff"))
            {
                DrawStaffTable();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Schedules"))
            {
                DrawSchedulesTable();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Streams"))
            {
                DrawStreamsTable();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawLocationsTable()
    {
        if (ImGui.Button("Create Location"))
        {
            if (!string.IsNullOrWhiteSpace(_venueId))
                _manageLocationWindow.OpenForCreate(_venueId);
        }
        ImGui.Spacing();

        var venue = _stateService.UserState.venues.FirstOrDefault(v => v.id == _venueId);
        var locations = venue?.locations ?? [];

        if (ImGui.BeginTable("EditVenueLocations", 3))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("House", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableHeadersRow();

            if (locations.Count > 0)
            {
                foreach (var loc in locations)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(loc.name);
                    
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(loc.house_id);

                    ImGui.TableNextColumn();
                    var manageLabel = $"Manage##Location{loc.id}";
                    if (ImGui.Button(manageLabel))
                    {
                        if (!string.IsNullOrWhiteSpace(_venueId))
                            _manageLocationWindow.OpenForEdit(_venueId, loc);
                    }

                    ImGui.SameLine();
                    var deleteLabel = $"Delete##Location{loc.id}";
                    if (ImGui.Button(deleteLabel))
                    {
                        ImGui.OpenPopup($"Confirm Location Delete##{loc.id}");
                    }

                    var popupId = $"Confirm Location Delete##{loc.id}";
                    if (ImGui.BeginPopupModal(popupId, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.TextUnformatted("Are you sure you want to delete this location? This action cannot be undone.");
                        ImGui.Separator();

                        if (ImGui.Button("Delete"))
                        {
                            _ = DeleteLocationAsync(loc.id);
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.SameLine();
                        if (ImGui.Button("Cancel"))
                        {
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }
                }
            }
            else
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("No locations");
            }

            ImGui.EndTable();
        }
    }

    private void DrawStaffTable()
    {
        if (ImGui.Button("Create Staff"))
        {
            if (!string.IsNullOrWhiteSpace(_venueId))
                _manageStaffWindow.OpenForCreate(_venueId);
        }
        ImGui.Spacing();

        var venue = _stateService.UserState.venues.FirstOrDefault(v => v.id == _venueId);
        var staff = venue?.staff ?? [];

        if (ImGui.BeginTable("EditVenueStaff", 4))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Position", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Granted", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableHeadersRow();

            if (staff.Count > 0)
            {
                foreach (var s in staff)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(string.IsNullOrEmpty(s.name) ? s.lodestone_id : s.name);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(string.IsNullOrEmpty(s.position) ? "—" : s.position);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(s.granted ? "Yes" : "No");

                    ImGui.TableNextColumn();
                    var manageLabel = $"Manage##Staff{s.id}";
                    if (ImGui.Button(manageLabel))
                    {
                        if (!string.IsNullOrWhiteSpace(_venueId))
                            _manageStaffWindow.OpenForEdit(_venueId, s);
                    }

                    ImGui.SameLine();
                    var deleteLabel = $"Delete##Staff{s.id}";
                    if (ImGui.Button(deleteLabel))
                    {
                        ImGui.OpenPopup($"Confirm Staff Delete##{s.id}");
                    }

                    var popupId = $"Confirm Staff Delete##{s.id}";
                    if (ImGui.BeginPopupModal(popupId, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.TextUnformatted("Are you sure you want to delete this staff member? This action cannot be undone.");
                        ImGui.Separator();

                        if (ImGui.Button("Delete"))
                        {
                            _ = DeleteStaffAsync(s.id);
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.SameLine();
                        if (ImGui.Button("Cancel"))
                        {
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }
                }
            }
            else
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("No staff");
            }

            ImGui.EndTable();
        }
    }

    private async Task DeleteStaffAsync(string staffId)
    {
        try
        {
            var result = await _staffApi.DestroyAsync(_venueId, staffId).ConfigureAwait(false);
            if (result.Success)
            {
                var user = await _accountApi.User().ConfigureAwait(false);
                if (user.Success)
                {
                    VenueSync.Messager.NotificationMessage("Staff deleted successfully", NotificationType.Success);
                }
                else
                {
                    VenueSync.Messager.NotificationMessage("Staff deleted, but failed to refresh user data", NotificationType.Warning);
                }
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Staff delete failed", NotificationType.Error);
                VenueSync.Log.Warning($"Staff delete failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Staff delete failed", NotificationType.Error);
            VenueSync.Log.Warning($"Staff delete exception: {ex.Message}");
        }
    }

    private async Task DeleteStreamAsync(string streamId)
    {
        try
        {
            var result = await _streamApi.DestroyAsync(_venueId, streamId).ConfigureAwait(false);
            if (result.Success)
            {
                var user = await _accountApi.User().ConfigureAwait(false);
                if (user.Success)
                {
                    VenueSync.Messager.NotificationMessage("Stream deleted successfully", NotificationType.Success);
                }
                else
                {
                    VenueSync.Messager.NotificationMessage("Stream deleted, but failed to refresh user data", NotificationType.Warning);
                }
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Stream delete failed", NotificationType.Error);
                VenueSync.Log.Warning($"Stream delete failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Stream delete failed", NotificationType.Error);
            VenueSync.Log.Warning($"Stream delete exception: {ex.Message}");
        }
    }

    private async Task DeleteScheduleAsync(string scheduleId)
    {
        try
        {
            var result = await _scheduleApi.DestroyAsync(_venueId, scheduleId).ConfigureAwait(false);
            if (result.Success)
            {
                var user = await _accountApi.User().ConfigureAwait(false);
                if (user.Success)
                {
                    VenueSync.Messager.NotificationMessage("Schedule deleted successfully", NotificationType.Success);
                }
                else
                {
                    VenueSync.Messager.NotificationMessage("Schedule deleted, but failed to refresh user data", NotificationType.Warning);
                }
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Schedule delete failed", NotificationType.Error);
                VenueSync.Log.Warning($"Schedule delete failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Schedule delete failed", NotificationType.Error);
            VenueSync.Log.Warning($"Schedule delete exception: {ex.Message}");
        }
    }

    private async Task DeleteLocationAsync(string locationId)
    {
        try
        {
            var result = await _locationApi.DestroyAsync(_venueId, locationId).ConfigureAwait(false);
            if (result.Success)
            {
                var user = await _accountApi.User().ConfigureAwait(false);
                if (user.Success)
                {
                    VenueSync.Messager.NotificationMessage("Location deleted successfully", NotificationType.Success);
                }
                else
                {
                    VenueSync.Messager.NotificationMessage("Location deleted, but failed to refresh user data", NotificationType.Warning);
                }
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Location delete failed", NotificationType.Error);
                VenueSync.Log.Warning($"Location delete failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Location delete failed", NotificationType.Error);
            VenueSync.Log.Warning($"Location delete exception: {ex.Message}");
        }
    }

    private void DrawSchedulesTable()
    {
        if (ImGui.Button("Create Schedule"))
        {
            if (!string.IsNullOrWhiteSpace(_venueId))
                _manageScheduleWindow.OpenForCreate(_venueId);
        }
        ImGui.Spacing();

        var venue = _stateService.UserState.venues.FirstOrDefault(v => v.id == _venueId);
        var schedules = venue?.schedules ?? [];

        if (ImGui.BeginTable("EditVenueSchedules", 4))
        {
            ImGui.TableSetupColumn("Day", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Open", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Close", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableHeadersRow();

            if (schedules.Count > 0)
            {
                foreach (var sc in schedules)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    var dayName = sc.day switch
                    {
                        0 => "Sunday",
                        1 => "Monday",
                        2 => "Tuesday",
                        3 => "Wednesday",
                        4 => "Thursday",
                        5 => "Friday",
                        6 => "Saturday",
                        _ => $"Day {sc.day}"
                    };
                    ImGui.TextUnformatted(dayName);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(string.IsNullOrEmpty(sc.start_time) ? "—" : sc.start_time);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(string.IsNullOrEmpty(sc.end_time) ? "—" : sc.end_time);

                    ImGui.TableNextColumn();
                    var manageLabel = $"Manage##Schedule{sc.id}";
                    if (ImGui.Button(manageLabel))
                    {
                        if (!string.IsNullOrWhiteSpace(_venueId))
                            _manageScheduleWindow.OpenForEdit(_venueId, sc);
                    }

                    ImGui.SameLine();
                    var deleteLabel = $"Delete##Schedule{sc.id}";
                    if (ImGui.Button(deleteLabel))
                    {
                        ImGui.OpenPopup($"Confirm Schedule Delete##{sc.id}");
                    }

                    var popupId = $"Confirm Schedule Delete##{sc.id}";
                    if (ImGui.BeginPopupModal(popupId, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.TextUnformatted("Are you sure you want to delete this schedule? This action cannot be undone.");
                        ImGui.Separator();

                        if (ImGui.Button("Delete"))
                        {
                            _ = DeleteScheduleAsync(sc.id);
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.SameLine();
                        if (ImGui.Button("Cancel"))
                        {
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }
                }
            }
            else
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("No schedules");
            }

            ImGui.EndTable();
        }
    }

    private void DrawStreamsTable()
    {
        if (ImGui.Button("Create Stream"))
        {
            if (!string.IsNullOrWhiteSpace(_venueId))
                _manageStreamWindow.OpenForCreate(_venueId);
        }
        ImGui.Spacing();

        var venue = _stateService.UserState.venues.FirstOrDefault(v => v.id == _venueId);
        var streams = venue?.streams ?? [];

        if (ImGui.BeginTable("EditVenueStreams", 6))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableHeadersRow();

            if (streams.Count > 0)
            {
                foreach (var st in streams)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(st.name);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(st.type);

                    ImGui.TableNextColumn();
                    var manageLabel = $"Manage##Stream{st.id}";
                    if (ImGui.Button(manageLabel))
                    {
                        if (!string.IsNullOrWhiteSpace(_venueId))
                            _manageStreamWindow.OpenForEdit(_venueId, st);
                    }

                    ImGui.SameLine();
                    var deleteLabel = $"Delete##Stream{st.id}";
                    if (ImGui.Button(deleteLabel))
                    {
                        ImGui.OpenPopup($"Confirm Stream Delete##{st.id}");
                    }

                    var popupId = $"Confirm Stream Delete##{st.id}";
                    if (ImGui.BeginPopupModal(popupId, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.TextUnformatted("Are you sure you want to delete this stream? This action cannot be undone.");
                        ImGui.Separator();

                        if (ImGui.Button("Delete"))
                        {
                            _ = DeleteStreamAsync(st.id);
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.SameLine();
                        if (ImGui.Button("Cancel"))
                        {
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }
                }
            }
            else
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted("No streams");
            }

            ImGui.EndTable();
        }
    }

    private void DrawGeneralSection()
    {
        DrawTextInput("Name", ref _name, 256, "Enter the venue name");
        DrawTextInput("Discord Invite", ref _discordInvite, 256, "Discord invite URL or code");
        DrawTextInput("Carrd URL", ref _carrdUrl, 512, "Carrd or other site URL");

        DrawMultilineInput("Description", ref _description, 2000, new Vector2(-1, 100));

        DrawTextInput("Tags (comma separated)", ref _tagsCsv, 512, "Example: music, rp, club");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var disabled = _isSubmitting || string.IsNullOrWhiteSpace(_name);
        if (disabled)
            ImGui.BeginDisabled();

        if (ImGui.Button(_isEditMode ? "Update Venue" : "Create Venue"))
        {
            _isSubmitting = true;
            _ = SubmitAsync();
            IsOpen = false;
        }

        if (disabled)
            ImGui.EndDisabled();
    }

    private void DrawTextInput(string label, ref string value, int maxLength, string? hint = null)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);

        var buffer = value;
        if (!string.IsNullOrEmpty(hint))
        {
            if (ImGui.InputTextWithHint($"##{label}", hint, ref buffer, maxLength))
            {
                value = buffer;
            }
        }
        else
        {
            if (ImGui.InputText($"##{label}", ref buffer, maxLength))
            {
                value = buffer;
            }
        }
    }

    private void DrawMultilineInput(string label, ref string value, int maxLength, Vector2 size)
    {
        ImGui.TextUnformatted(label);
        var buffer = value;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline($"##{label}", ref buffer, maxLength, size))
        {
            value = buffer;
        }
    }

    private void DrawLogoSection()
    {
        if (!_isEditMode)
            return;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Venue Logo");
        if (!string.IsNullOrEmpty(_selectedLogoPath))
        {
            ImGui.TextUnformatted($"Selected: {_selectedLogoPath}");
        }

        var disableSelect = _isUploadingLogo;
        if (disableSelect) ImGui.BeginDisabled();
        if (ImGui.Button("Select Logo..."))
        {
            _fileDialog.OpenFileDialog(
                "Select Venue Logo",
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

        var canUpload = !_isUploadingLogo && !string.IsNullOrWhiteSpace(_venueId) && File.Exists(_selectedLogoPath);
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
            VenueSync.Log.Warning($"Failed to load logo preview: {ex.Message}");
        }
    }

    private Task TryLoadExistingLogoAsync(string fileId, string hash)
    {
        if (string.IsNullOrWhiteSpace(_venueId) || string.IsNullOrWhiteSpace(fileId))
            return Task.CompletedTask;

        _syncFileService.MaybeDownloadFile(
            _venueId,
            fileId,
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
            if (string.IsNullOrWhiteSpace(_venueId) || !File.Exists(_selectedLogoPath))
            {
                VenueSync.Messager.NotificationMessage("No logo selected", NotificationType.Error);
                return;
            }

            await using var stream = File.OpenRead(_selectedLogoPath);
            var fileName = Path.GetFileName(_selectedLogoPath);
            var contentType = GetContentType(_selectedLogoPath);

            var result = await _venueApi.UploadLogoAsync(_venueId, stream, fileName, contentType).ConfigureAwait(false);
            if (result is { Success: true, Data: not null })
            {
                var venues = _stateService.UserState.venues;
                var updated = result.Data;
                var idx = venues.FindIndex(v => v.id == updated.id);
                if (idx >= 0)
                    venues[idx] = updated;
                else
                    venues.Add(updated);

                _currentLogoFileId = updated.logo;
                _currentLogoHash = updated.hash;
                _selectedLogoPath = string.Empty;

                VenueSync.Messager.NotificationMessage("Logo uploaded successfully", NotificationType.Success);
                await TryLoadExistingLogoAsync(_currentLogoFileId, _currentLogoHash);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Logo upload failed", NotificationType.Error);
                VenueSync.Log.Warning($"Logo upload failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Logo upload failed", NotificationType.Error);
            VenueSync.Log.Warning($"Logo upload exception: {ex.Message}");
        }
        finally
        {
            _isUploadingLogo = false;
        }
    }

    private async Task SubmitAsync()
    {
        var tags = _tagsCsv
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        var payload = new
        {
            name = _name,
            description = _description,
            discord_invite = _discordInvite,
            carrd_url = _carrdUrl,
            tags
        };

        try
        {
            ApiResult<UserVenueItem> result;
            if (_isEditMode && !string.IsNullOrWhiteSpace(_venueId))
            {
                result = await _venueApi.UpdateAsync(_venueId, payload).ConfigureAwait(false);
            }
            else
            {
                result = await _venueApi.StoreAsync(payload).ConfigureAwait(false);
            }

            if (result is { Success: true, Data: not null })
            {
                var venues = _stateService.UserState.venues;
                var updated = result.Data;
                var idx = venues.FindIndex(v => v.id == updated.id);
                if (idx >= 0)
                    venues[idx] = updated;
                else
                    venues.Add(updated);

                VenueSync.Messager.NotificationMessage(_isEditMode ? "Venue updated successfully" : "Venue created successfully", NotificationType.Success);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Venue save failed", NotificationType.Error);
                VenueSync.Log.Warning($"Venue submit failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Venue save failed", NotificationType.Error);
            VenueSync.Log.Warning($"Venue submit exception: {ex.Message}");
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
