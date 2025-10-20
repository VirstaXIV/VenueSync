using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using OtterGui.Classes;
using OtterGui.Text;
using OtterGui.Widgets;
using VenueSync.Services;
using VenueSync.Services.Api;
using VenueSync.State;
using VenueSync.Ui.Crud;

namespace VenueSync.Ui.Tabs;

public class VenuesTab(StateService stateService, ManageVenueWindow manageVenueWindow, VenueApi venueApi, AccountApi accountApi) : ITab
{
    public ReadOnlySpan<byte> Label => "Venues"u8;

    public void DrawContent()
    {
        using var child = ImUtf8.Child("MainWindowChild"u8, default);
        if (!child)
            return;

        if (ImUtf8.Button("Create Venue"))
        {
            manageVenueWindow.OpenForCreate();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        using (ImUtf8.Child("VenuesChild"u8, default))
        {
            DrawVenuesTable();
        }
    }

    private Vector4 GetVenueColor(UserVenueItem venue)
    {
        if (venue.id == stateService.VenueState.id)
        {
            return new Vector4(0, 1, 0, 1);
        }

        return new Vector4(1, 1, 1, 1);
    }

    private void DrawVenuesTable()
    {
        if (ImGui.BeginTable("Venues", 2))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableHeadersRow();

            if (stateService.HasVenues())
            {
                foreach (var venue in stateService.UserState.venues)
                {
                    var color = GetVenueColor(venue);

                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, venue.name);

                    ImGui.TableNextColumn();
                    var manageLabel = $"Manage##{venue.id}";
                    if (ImGui.Button(manageLabel))
                    {
                        manageVenueWindow.OpenForEdit(venue);
                    }

                    ImGui.SameLine();
                    var deleteLabel = $"Delete##{venue.id}";
                    if (ImGui.Button(deleteLabel))
                    {
                        ImGui.OpenPopup($"Confirm Delete##{venue.id}");
                    }

                    var popupId = $"Confirm Delete##{venue.id}";
                    if (ImGui.BeginPopupModal(popupId, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.TextUnformatted("Are you sure you want to delete this venue? This action cannot be undone.");
                        ImGui.Separator();

                        if (ImGui.Button("Delete"))
                        {
                            _ = DeleteVenueAsync(venue.id);
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
                ImGui.TableSetColumnIndex(0);
                ImGui.TextColored(new Vector4(1, 1, 1, 1), "No Venues");
            }
            
            ImGui.EndTable();
        }
    }
        
    private async Task DeleteVenueAsync(string venueId)
    {
        try
        {
            var result = await venueApi.DestroyAsync(venueId).ConfigureAwait(false);
            if (result.Success)
            {
                var user = await accountApi.User().ConfigureAwait(false);
                if (user.Success)
                {
                    VenueSync.Messager.NotificationMessage("Venue deleted successfully", NotificationType.Success);
                }
                else
                {
                    VenueSync.Messager.NotificationMessage("Venue deleted, but failed to refresh user data", NotificationType.Warning);
                }
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Venue delete failed", NotificationType.Error);
                VenueSync.Log.Warning($"Venue delete failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Venue delete failed", NotificationType.Error);
            VenueSync.Log.Warning($"Venue delete exception: {ex.Message}");
        }
    }

}