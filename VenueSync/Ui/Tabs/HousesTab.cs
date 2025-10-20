using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using OtterGui.Text;
using OtterGui.Widgets;
using VenueSync.Services;
using VenueSync.State;
using VenueSync.Ui.Crud;

namespace VenueSync.Ui.Tabs;

public class HousesTab(StateService stateService, HouseVerifyWindow houseVerifyWindow, ManageMannequinsWindow manageMannequinsWindow): ITab
{
    public ReadOnlySpan<byte> Label => "Houses"u8;
    
    private bool _isInOwnedHouse = false;

    public void DrawContent()
    {
        _isInOwnedHouse = IsInOwnedHouse();
        
        using var child = ImUtf8.Child("MainWindowChild"u8, default);
        if (!child)
            return;
        
        DrawActionButtons();

        using (ImUtf8.Child("HousesChild"u8, default))
        {
            DrawHousesTable();
        }
    }

    private void DrawActionButtons()
    {
        if (ImUtf8.Button("Start House Verification"))
        {
            houseVerifyWindow.Toggle();
        }
        ImGui.SameLine();
        ImGui.BeginDisabled(!_isInOwnedHouse);
        if (ImUtf8.Button("Manage Mannequins"))
        {
            manageMannequinsWindow.Toggle();
        }
        ImGui.EndDisabled();

        if (!_isInOwnedHouse)
        {
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 40f);
                ImGui.TextUnformatted("Must be in an owned or granted house.");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private bool IsInOwnedHouse()
    {
        if (!stateService.HasHouses())
        {
            return false;
        }
        foreach (var house in stateService.UserState.houses)
        {
            if (house.district == stateService.CurrentHouse.District && house.ward == stateService.CurrentHouse.Ward && house.plot == stateService.CurrentHouse.Plot)
            {
                return true;
            }
        }

        return false;
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
        if (ImGui.BeginTable("Houses", 5))
        {
            ImGui.TableSetupColumn("Owner", ImGuiTableColumnFlags.WidthFixed, 160);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("Perms", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Address", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Verified", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();
            
            if (stateService.HasHouses())
            {
                foreach (var house in stateService.UserState.houses)
                {
                    var address = $"{house.district} {house.ward}/{house.plot} {house.world} [{house.data_center}]";
                    var color = GetHouseColor(house);
                    
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, house.owner);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, house.type);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, house.perms ? "Yes" : "No");
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, address);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, house.verified ? "Yes" : "No");
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
