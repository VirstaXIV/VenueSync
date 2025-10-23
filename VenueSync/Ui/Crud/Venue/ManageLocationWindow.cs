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

    // Mods management state
    private string _selectedMannequinId = string.Empty;
    private string _selectedModId = string.Empty;
    private bool _modSubmitting;

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
        _selectedMannequinId = string.Empty;
        _selectedModId = string.Empty;
        IsOpen = true;
    }

    public void OpenForEdit(string venueId, UserVenueLocationItem location)
    {
        _isEditMode = true;
        _venueId = venueId;
        _locationId = location.id;
        _name = location.name;
        _houseId = location.house_id;
        _selectedMannequinId = string.Empty;
        _selectedModId = string.Empty;
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

        if (_isEditMode && !string.IsNullOrWhiteSpace(_venueId) && !string.IsNullOrWhiteSpace(_locationId))
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawModsManager();
        }
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
                    // reset mannequin selection if house changed
                    _selectedMannequinId = string.Empty;
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }

    private void DrawModsManager()
    {
        ImGui.TextUnformatted("Add Mod to Location");
        ImGui.Spacing();

        // Select Mannequin
        ImGui.TextUnformatted("Mannequin");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180);
        DrawMannequinSelector();

        ImGui.SameLine();

        // Select Mod
        ImGui.TextUnformatted("Mod");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        DrawUserModSelector();

        var canAdd = !_modSubmitting
                     && !string.IsNullOrWhiteSpace(_houseId)
                     && !string.IsNullOrWhiteSpace(_selectedMannequinId)
                     && !string.IsNullOrWhiteSpace(_selectedModId);
        if (!canAdd) ImGui.BeginDisabled();
        if (ImGui.Button("Add Mod"))
        {
            _modSubmitting = true;
            _ = AddModToLocationAsync();
        }
        if (!canAdd) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawExistingModsTable();
    }

    private void DrawMannequinSelector()
    {
        var house = _stateService.UserState.houses.FirstOrDefault(h => h.id == _houseId);
        var mannequins = house?.mannequins ?? [];

        var currentLabel = string.IsNullOrWhiteSpace(_selectedMannequinId)
            ? "Select Mannequin"
            : (mannequins.FirstOrDefault(m => m.id == _selectedMannequinId)?.name ?? _selectedMannequinId);

        if (ImGui.BeginCombo("##LocationMannequin", currentLabel))
        {
            foreach (var m in mannequins)
            {
                var selected = _selectedMannequinId == m.id;
                if (ImGui.Selectable(m.name, selected))
                {
                    _selectedMannequinId = m.id;
                }
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }

    private void DrawUserModSelector()
    {
        var mods = _stateService.ModsState.modList;
        var currentLabel = string.IsNullOrWhiteSpace(_selectedModId)
            ? "Select Mod"
            : (mods.FirstOrDefault(m => m.id == _selectedModId)?.name ?? _selectedModId);

        if (ImGui.BeginCombo("##LocationMod", currentLabel))
        {
            foreach (var mod in mods)
            {
                var selected = _selectedModId == mod.id;
                if (ImGui.Selectable(mod.name, selected))
                {
                    _selectedModId = mod.id;
                }
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }

    private void DrawExistingModsTable()
    {
        var venues = _stateService.UserState.venues;
        var venue = venues.FirstOrDefault(v => v.id == _venueId);
        var location = venue?.locations.FirstOrDefault(l => l.id == _locationId);

        if (location == null)
        {
            ImGui.TextUnformatted("No location data available.");
            return;
        }

        var house = _stateService.UserState.houses.FirstOrDefault(h => h.id == _houseId);
        string GetMannequinName(string mid) =>
            house?.mannequins.FirstOrDefault(m => m.id == mid)?.name ?? mid;

        if (location.mods.Count == 0)
        {
            ImGui.TextUnformatted("No mods added yet.");
            return;
        }

        if (ImGui.BeginTable("LocationModsTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Mod");
            ImGui.TableSetupColumn("Mannequin");
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 180);
            ImGui.TableHeadersRow();

            foreach (var mod in location.mods.ToList())
            {
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(mod.name);

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted(GetMannequinName(mod.mannequin_id));

                ImGui.TableSetColumnIndex(2);

                var toggleLabel = $"Toggle##{mod.id}";
                if (!_modSubmitting && ImGui.Button(toggleLabel))
                {
                    _modSubmitting = true;
                    _ = ToggleLocationModAsync(mod.id);
                }

                ImGui.SameLine();

                var deleteBtnLabel = $"Delete##{mod.id}";
                if (ImGui.Button(deleteBtnLabel))
                {
                    ImGui.OpenPopup($"ConfirmDelete##{mod.id}");
                }

                bool open = true;
                if (ImGui.BeginPopupModal($"ConfirmDelete##{mod.id}", ref open, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.TextUnformatted("Are you sure you want to delete this mod from the location?");
                    ImGui.Spacing();

                    if (!_modSubmitting && ImGui.Button("Yes"))
                    {
                        _modSubmitting = true;
                        _ = DeleteLocationModAsync(mod.id);
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("No"))
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
            }

            ImGui.EndTable();
        }
    }

    private async Task AddModToLocationAsync()
    {
        try
        {
            var result = await _locationApi.AddModAsync(_venueId, _locationId, _selectedModId, _selectedMannequinId).ConfigureAwait(false);
            if (result is { Success: true, Data: not null })
            {
                var modItem = result.Data;
                var venue = _stateService.UserState.venues.FirstOrDefault(v => v.id == _venueId);
                var loc = venue?.locations.FirstOrDefault(l => l.id == _locationId);
                if (loc != null)
                {
                    var idx = loc.mods.FindIndex(m => m.id == modItem.id);
                    if (idx >= 0) loc.mods[idx] = modItem;
                    else loc.mods.Add(modItem);
                }

                _selectedModId = string.Empty;
                _selectedMannequinId = string.Empty;

                VenueSync.Messager.NotificationMessage("Mod added to location", NotificationType.Success);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Failed to add mod to location", NotificationType.Error);
                VenueSync.Log.Warning($"AddModToLocation failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Failed to add mod to location", NotificationType.Error);
            VenueSync.Log.Warning($"AddModToLocation exception: {ex.Message}");
        }
        finally
        {
            _modSubmitting = false;
        }
    }

    private async Task ToggleLocationModAsync(string locationModId)
    {
        try
        {
            var result = await _locationApi.ToggleModAsync(_venueId, _locationId, locationModId).ConfigureAwait(false);
            if (result.Success)
            {
                if (result.Data is not null)
                {
                    var venue = _stateService.UserState.venues.FirstOrDefault(v => v.id == _venueId);
                    var loc = venue?.locations.FirstOrDefault(l => l.id == _locationId);
                    if (loc != null)
                    {
                        var idx = loc.mods.FindIndex(m => m.id == result.Data.id);
                        if (idx >= 0) loc.mods[idx] = result.Data;
                    }
                }
                VenueSync.Messager.NotificationMessage("Toggled mod state", NotificationType.Success);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Failed to toggle mod", NotificationType.Error);
                VenueSync.Log.Warning($"ToggleLocationMod failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Failed to toggle mod", NotificationType.Error);
            VenueSync.Log.Warning($"ToggleLocationMod exception: {ex.Message}");
        }
        finally
        {
            _modSubmitting = false;
        }
    }

    private async Task DeleteLocationModAsync(string locationModId)
    {
        try
        {
            var result = await _locationApi.DeleteModAsync(_venueId, _locationId, locationModId).ConfigureAwait(false);
            if (result.Success)
            {
                var venue = _stateService.UserState.venues.FirstOrDefault(v => v.id == _venueId);
                var loc = venue?.locations.FirstOrDefault(l => l.id == _locationId);
                if (loc != null)
                {
                    while (true)
                    {
                        var idx = loc.mods.FindIndex(m => m.id == locationModId);
                        if (idx < 0) break;
                        loc.mods.RemoveAt(idx);
                    }
                }

                VenueSync.Messager.NotificationMessage("Removed mod from location", NotificationType.Success);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Failed to remove mod", NotificationType.Error);
                VenueSync.Log.Warning($"DeleteLocationMod failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Failed to remove mod", NotificationType.Error);
            VenueSync.Log.Warning($"DeleteLocationMod exception: {ex.Message}");
        }
        finally
        {
            _modSubmitting = false;
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
