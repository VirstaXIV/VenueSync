using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using OtterGui.Classes;
using VenueSync.Events;
using VenueSync.Services;
using VenueSync.Services.Api.Venue;
using VenueSync.State;

namespace VenueSync.Ui.Crud.Venue;

public class ManageStaffWindow : Window, IDisposable
{
    private readonly StateService _stateService;
    private readonly StaffApi _staffApi;

    private bool _isEditMode;
    private string _venueId = string.Empty;
    private string _staffId = string.Empty;
    private string _lodestoneId = string.Empty;
    private string _position = string.Empty;
    private bool _granted;
    private bool _isSubmitting;

    public ManageStaffWindow(StateService stateService, StaffApi staffApi, ServiceDisconnected serviceDisconnected) : base("Manage Staff")
    {
        _stateService = stateService;
        _staffApi = staffApi;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 200),
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
        _staffId = string.Empty;
        _lodestoneId = string.Empty;
        _position = string.Empty;
        _granted = false;
        IsOpen = true;
    }

    public void OpenForEdit(string venueId, UserVenueStaffItem staff)
    {
        _isEditMode = true;
        _venueId = venueId;
        _staffId = staff.id;
        _lodestoneId = staff.lodestone_id;
        _position = staff.position;
        _granted = staff.granted;

        IsOpen = true;
    }

    public override void PreDraw()
    {
        WindowName = _isEditMode ? "Edit Staff###ManageStaffWindow" : "Create Staff###ManageStaffWindow";
    }

    public override void Draw()
    {
        ImGui.TextUnformatted(_isEditMode ? "Edit staff member" : "Create a new staff member");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Lodestone ID");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var lodestoneBuf = _lodestoneId;
        if (ImGui.InputText("##StaffLodestoneId", ref lodestoneBuf, 64))
            _lodestoneId = lodestoneBuf;

        ImGui.TextUnformatted("Role");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var posBuf = _position;
        if (ImGui.InputText("##StaffRole", ref posBuf, 200))
            _position = posBuf;

        ImGui.Spacing();
        ImGui.Checkbox("Granted", ref _granted);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var canSubmit = !_isSubmitting && !string.IsNullOrWhiteSpace(_venueId) && (_isEditMode || !string.IsNullOrWhiteSpace(_lodestoneId));
        if (!canSubmit) ImGui.BeginDisabled();
        if (ImGui.Button(_isEditMode ? "Update Staff" : "Create Staff"))
        {
            _isSubmitting = true;
            _ = SubmitAsync();
            IsOpen = false;
        }
        if (!canSubmit) ImGui.EndDisabled();
    }

    private async Task SubmitAsync()
    {
        var payload = new System.Collections.Generic.Dictionary<string, object?>()
        {
            ["lodestone_id"] = _lodestoneId,
            ["position"] = _position,
            ["granted"] = _granted,
        };

        try
        {
            ApiResult<UserVenueStaffItem> result;
            if (_isEditMode)
            {
                var staffId = _staffId;
                result = await _staffApi.UpdateAsync(_venueId, staffId, payload).ConfigureAwait(false);
            }
            else
            {
                result = await _staffApi.StoreAsync(_venueId, payload).ConfigureAwait(false);
            }

            if (result is { Success: true, Data: not null })
            {
                var staffItem = result.Data;
                var venues = _stateService.UserState.venues;
                var venue = venues.FirstOrDefault(v => v.id == _venueId);
                if (venue is not null)
                {
                    var list = venue.staff;
                    var sIdx = list.FindIndex(s => s.id == staffItem.id);
                    if (sIdx >= 0)
                        list[sIdx] = staffItem;
                    else
                        list.Add(staffItem);
                }

                _staffId = staffItem.id;

                VenueSync.Messager.NotificationMessage(_isEditMode ? "Staff updated successfully" : "Staff created successfully", NotificationType.Success);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Staff save failed", NotificationType.Error);
                VenueSync.Log.Warning($"Staff submit failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Staff save failed", NotificationType.Error);
            VenueSync.Log.Warning($"Staff submit exception: {ex.Message}");
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
    }
}
