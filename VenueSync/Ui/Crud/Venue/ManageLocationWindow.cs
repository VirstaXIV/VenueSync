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

public class ManageLocationWindow : Window, IDisposable
{
    private readonly StateService _stateService;
    private readonly LocationApi _locationApi;

    private bool _isEditMode;
    private string _venueId = string.Empty;
    private string _locationId = string.Empty;

    private string _name = string.Empty;
    private string _houseId = string.Empty;
    private bool _isSubmitting;

    public ManageLocationWindow(StateService stateService, LocationApi locationApi, ServiceDisconnected serviceDisconnected) : base("Manage Location")
    {
        _stateService = stateService;
        _locationApi = locationApi;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 220),
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
        _locationId = string.Empty;
        _name = string.Empty;
        _houseId = string.Empty;
        IsOpen = true;
    }

    public void OpenForEdit(string venueId, UserVenueLocationItem location)
    {
        _isEditMode = true;
        _venueId = venueId;
        _locationId = location.id;
        _name = location.name;
        _houseId = location.house_id;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        WindowName = _isEditMode ? "Edit Location###ManageLocationWindow" : "Create Location###ManageLocationWindow";
    }

    public override void Draw()
    {
        ImGui.TextUnformatted(_isEditMode ? "Edit location" : "Create a new location");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Name");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var nameBuf = _name;
        if (ImGui.InputText("##LocationName", ref nameBuf, 128))
            _name = nameBuf;

        ImGui.TextUnformatted("House");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        DrawHouseSelector();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var canSubmit = !_isSubmitting && !string.IsNullOrWhiteSpace(_venueId) &&
                        (_isEditMode || (!string.IsNullOrWhiteSpace(_name) && !string.IsNullOrWhiteSpace(_houseId)));
        if (!canSubmit) ImGui.BeginDisabled();
        if (ImGui.Button(_isEditMode ? "Update Location" : "Create Location"))
        {
            _isSubmitting = true;
            _ = SubmitAsync();
            IsOpen = false;
        }
        if (!canSubmit) ImGui.EndDisabled();
    }

    private void DrawHouseSelector()
    {
        var options = _stateService.UserState.houses
            .Where(h => h.perms)
            .Select(h => (
                value: h.id,
                label: $"{h.district} {h.ward}/{h.plot} {h.world} [{h.data_center}]"
            ))
            .ToList();

        if (!string.IsNullOrWhiteSpace(_houseId) && options.All(o => o.value != _houseId))
        {
            options.Insert(0, (value: _houseId, label: _houseId));
        }

        var currentLabel = string.IsNullOrWhiteSpace(_houseId)
            ? "Select House"
            : (options.FirstOrDefault(o => o.value == _houseId).label ?? _houseId);

        if (ImGui.BeginCombo("##LocationHouse", currentLabel))
        {
            foreach (var opt in options)
            {
                var selected = _houseId == opt.value;
                if (ImGui.Selectable(opt.label, selected))
                {
                    _houseId = opt.value;
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }

    private async Task SubmitAsync()
    {
        var payload = new System.Collections.Generic.Dictionary<string, object?>()
        {
            ["name"] = _name,
            ["house_id"] = _houseId,
        };

        try
        {
            ApiResult<UserVenueLocationItem> result;
            if (_isEditMode)
            {
                var locationId = _locationId;
                result = await _locationApi.UpdateAsync(_venueId, locationId, payload).ConfigureAwait(false);
            }
            else
            {
                result = await _locationApi.StoreAsync(_venueId, payload).ConfigureAwait(false);
            }

            if (result is { Success: true, Data: not null })
            {
                var locationItem = result.Data;
                var venues = _stateService.UserState.venues;
                var venue = venues.FirstOrDefault(v => v.id == _venueId);
                if (venue is not null)
                {
                    var list = venue.locations;
                    var idx = list.FindIndex(l => l.id == locationItem.id);
                    if (idx >= 0)
                        list[idx] = locationItem;
                    else
                        list.Add(locationItem);
                }

                _locationId = locationItem.id;

                VenueSync.Messager.NotificationMessage(_isEditMode ? "Location updated successfully" : "Location created successfully", NotificationType.Success);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Location save failed", NotificationType.Error);
                VenueSync.Log.Warning($"Location submit failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Location save failed", NotificationType.Error);
            VenueSync.Log.Warning($"Location submit exception: {ex.Message}");
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
