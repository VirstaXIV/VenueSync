using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using OtterGui.Text;
using OtterGui.Widgets;
using VenueSync.Services;
using VenueSync.State;

namespace VenueSync.Ui.Tabs.VenuesTab;

public class VenuesTab(StateService stateService): ITab
{
    public ReadOnlySpan<byte> Label => "Venues"u8;

    public void DrawContent()
    {
        using var child = ImUtf8.Child("MainWindowChild"u8, default);
        if (!child)
            return;

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

        return new Vector4(1,1,1,1);
    }

    private void DrawVenuesTable()
    {
        if (ImGui.BeginTable("Venues", 1))
        {
            ImGui.TableSetupColumn("Name");
            ImGui.TableHeadersRow();
            
            if (stateService.HasVenues())
            {
                foreach (var venue in stateService.UserState.venues)
                {
                    var color = GetVenueColor(venue);
                    
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, venue.name);
                }
            }
            else
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextColored(new Vector4(1,1,1,1), "No Venues");
            }
            
            ImGui.EndTable();
        }
    }
        
}