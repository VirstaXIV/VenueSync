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
using VenueSync.Services.Api;
using VenueSync.State;

namespace VenueSync.Ui.Crud;

public class ManageHouseWindow : Window, IDisposable
{
    private readonly StateService _stateService;
    private readonly HouseApi _houseApi;
    private readonly AccountApi _accountApi;

    private string _houseId = string.Empty;
    private string _lodestoneInput = string.Empty;
    private bool _isSubmitting;

    public ManageHouseWindow(
        StateService stateService,
        HouseApi houseApi,
        AccountApi accountApi,
        ServiceDisconnected serviceDisconnected
    ) : base("Manage House")
    {
        _stateService = stateService;
        _houseApi = houseApi;
        _accountApi = accountApi;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 320),
            MaximumSize = new Vector2(1000, 800),
        };

        RespectCloseHotkey = true;
        IsOpen = false;

        serviceDisconnected.Subscribe(OnDisconnect, ServiceDisconnected.Priority.High);
    }

    private void OnDisconnect()
    {
        if (IsOpen)
            Toggle();
    }

    public void OpenForHouse(UserHouseItem house)
    {
        _houseId = house.id;
        _lodestoneInput = string.Empty;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        WindowName = "Manage House Grants###ManageHouseWindow";
    }

    public override void Draw()
    {
        if (string.IsNullOrWhiteSpace(_houseId))
        {
            ImGui.TextUnformatted("No house selected.");
            return;
        }

        ImGui.TextUnformatted("Grants");
        ImGui.Separator();
        ImGui.Spacing();

        DrawGrantInput();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawGrantsTable();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawGrantInput()
    {
        ImGui.TextUnformatted("Lodestone ID");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-200);
        var buffer = _lodestoneInput;
        if (ImGui.InputTextWithHint("##GrantLodestoneId", "Enter lodestone id", ref buffer, 64))
        {
            _lodestoneInput = buffer;
        }
        ImGui.SameLine();
        var canAdd = !_isSubmitting && !string.IsNullOrWhiteSpace(_lodestoneInput);
        if (!canAdd) ImGui.BeginDisabled();
        if (ImGui.Button("Add"))
        {
            _isSubmitting = true;
            _ = AddGrantAsync(_lodestoneInput);
        }
        if (!canAdd) ImGui.EndDisabled();
    }

    private void DrawGrantsTable()
    {
        var house = _stateService.UserState.houses.FirstOrDefault(h => h.id == _houseId);
        var grants = house?.grants ?? [];

        if (ImGui.BeginTable("HouseGrantsTable", 3, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Owner Lodestone ID", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Granted Lodestone ID", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableHeadersRow();

            if (grants.Count > 0)
            {
                foreach (var g in grants)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(string.IsNullOrEmpty(g.owner_lodestone_id) ? "—" : g.owner_lodestone_id);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(string.IsNullOrEmpty(g.granted_lodestone_id) ? "—" : g.granted_lodestone_id);

                    ImGui.TableNextColumn();
                    var deleteLabel = $"Delete##Grant{g.id}";
                    if (ImGui.Button(deleteLabel))
                    {
                        ImGui.OpenPopup($"Confirm Grant Delete##{g.id}");
                    }

                    var popupId = $"Confirm Grant Delete##{g.id}";
                    if (ImGui.BeginPopupModal(popupId, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.TextUnformatted("Are you sure you want to delete this grant? This action cannot be undone.");
                        ImGui.Separator();

                        if (ImGui.Button("Delete"))
                        {
                            _ = DeleteGrantAsync(g.id);
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
                ImGui.TextUnformatted("No grants");
            }

            ImGui.EndTable();
        }
    }

    private async Task AddGrantAsync(string lodestoneId)
    {
        try
        {
            var result = await _houseApi.StoreGrantAsync(_houseId, lodestoneId).ConfigureAwait(false);
            if (result.Success)
            {
                var user = await _accountApi.User().ConfigureAwait(false);
                if (user.Success)
                {
                    VenueSync.Messager.NotificationMessage("Grant added successfully", NotificationType.Success);
                    _lodestoneInput = string.Empty;
                }
                else
                {
                    VenueSync.Messager.NotificationMessage("Grant added, but failed to refresh user data", NotificationType.Warning);
                }
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Grant add failed", NotificationType.Error);
                VenueSync.Log.Warning($"Grant add failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Grant add failed", NotificationType.Error);
            VenueSync.Log.Warning($"Grant add exception: {ex.Message}");
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private async Task DeleteGrantAsync(string grantId)
    {
        try
        {
            var result = await _houseApi.DestroyGrantAsync(_houseId, grantId).ConfigureAwait(false);
            if (result.Success)
            {
                var user = await _accountApi.User().ConfigureAwait(false);
                if (user.Success)
                {
                    VenueSync.Messager.NotificationMessage("Grant deleted successfully", NotificationType.Success);
                }
                else
                {
                    VenueSync.Messager.NotificationMessage("Grant deleted, but failed to refresh user data", NotificationType.Warning);
                }
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Grant delete failed", NotificationType.Error);
                VenueSync.Log.Warning($"Grant delete failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Grant delete failed", NotificationType.Error);
            VenueSync.Log.Warning($"Grant delete exception: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (IsOpen)
            Toggle();
    }
}
