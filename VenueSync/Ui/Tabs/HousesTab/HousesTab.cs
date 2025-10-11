using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using OtterGui.Text;
using OtterGui.Widgets;
using VenueSync.Services;
using VenueSync.State;

namespace VenueSync.Ui.Tabs.HousesTab;

public class HousesTab(StateService stateService, HouseVerifyWindow houseVerifyWindow): ITab
{
    public ReadOnlySpan<byte> Label => "Houses"u8;

    public void DrawContent()
    {
        using var child = ImUtf8.Child("MainWindowChild"u8, default);
        if (!child)
            return;

        if (ImUtf8.Button("Start House Verification"))
        {
            houseVerifyWindow.Toggle();
        }

        using (ImUtf8.Child("HousesChild"u8, default))
        {
            DrawHousesTable();
        }
    }
    
    private Vector4 GetHouseColor(UserHouseItem house)
    {
        if (house.world == stateService.CurrentHouse.WorldName)
        {
            if (house.district == stateService.CurrentHouse.District && house.ward == stateService.CurrentHouse.Ward && house.plot == stateService.CurrentHouse.Plot)
            {
                return new Vector4(0, 1, 0, 1);
            }
        }

        return new Vector4(1,1,1,1);
    }

    private void DrawHousesTable()
    {
        if (ImGui.BeginTable("Houses", 2))
        {
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("Address");
            ImGui.TableHeadersRow();
            
            if (stateService.HasHouses())
            {
                foreach (var house in stateService.UserState.houses)
                {
                    var address = $"{house.district} {house.ward}/{house.plot} {house.world} [{house.data_center}]";
                    var color = GetHouseColor(house);
                    
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, house.name);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, address);
                }
            }
            else
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextColored(new Vector4(1,1,1,1), "No Houses");
            }
            
            ImGui.EndTable();
        }
    }
        
}
