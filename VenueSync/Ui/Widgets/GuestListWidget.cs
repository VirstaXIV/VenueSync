using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using OtterGui.Services;
using VenueSync.Data;
using VenueSync.Services;

namespace VenueSync.Ui.Widgets;

public class GuestListWidget: IService
{
    private readonly ChatService _chatService;
    private readonly StateService _stateService;
    
    public GuestListWidget(ChatService chatService, StateService stateService)
    {
        _chatService = chatService;
        _stateService = stateService;
    }
    
    private List<KeyValuePair<string, Player>> GetSortedGuests(ImGuiTableSortSpecsPtr sortSpecs)
    {
        ImGuiTableColumnSortSpecsPtr currentSpecs = sortSpecs.Specs;

        var guestList = _stateService.VisitorsState.players.ToList();
        
        guestList.Sort((pair1, pair2) => {
            if (_stateService.VisitorsState.SortCurrentVisitorsTop && pair1.Value.InHouse != pair2.Value.InHouse) {
                return pair2.Value.InHouse.CompareTo(pair1.Value.InHouse);
            }
            else
            {
                switch (currentSpecs.ColumnIndex)
                {
                    case 0: // Latest Entry
                        switch (currentSpecs.SortDirection)
                        {
                            case ImGuiSortDirection.Descending:
                                return pair2.Value.LatestEntry.CompareTo(pair1.Value.LatestEntry);
                            case ImGuiSortDirection.Ascending:
                                return pair1.Value.LatestEntry.CompareTo(pair2.Value.LatestEntry);
                        }
                        break;
                    case 1: // Name
                        switch (currentSpecs.SortDirection)
                        {
                            case ImGuiSortDirection.Descending:
                                return pair2.Value.Name.CompareTo(pair1.Value.Name);
                            case ImGuiSortDirection.Ascending:
                                return pair1.Value.Name.CompareTo(pair2.Value.Name);
                        }
                        break;
                    case 2: // Entry Count
                        switch (currentSpecs.SortDirection)
                        {
                            case ImGuiSortDirection.Descending:
                                return pair2.Value.EntryCount.CompareTo(pair1.Value.EntryCount);
                            case ImGuiSortDirection.Ascending:
                                return pair1.Value.EntryCount.CompareTo(pair2.Value.EntryCount);
                        }
                        break;
                    case 3: // Last Roll
                        switch (currentSpecs.SortDirection)
                        {
                            case ImGuiSortDirection.Descending:
                                return pair2.Value.LastRoll.CompareTo(pair1.Value.LastRoll);
                            case ImGuiSortDirection.Ascending:
                                return pair1.Value.LastRoll.CompareTo(pair2.Value.LastRoll);
                        }
                        break;
                    case 4: // Minutes Inside
                        switch (currentSpecs.SortDirection)
                        {
                            case ImGuiSortDirection.Descending:
                                return pair2.Value.MilisecondsInHouse.CompareTo(pair1.Value.MilisecondsInHouse);
                            case ImGuiSortDirection.Ascending:
                                return pair1.Value.MilisecondsInHouse.CompareTo(pair2.Value.MilisecondsInHouse);
                        }
                        break;
                    default:
                        break;
                }
            }
            return 0;
        });
        
        return guestList;
    }
    
    public void Draw()
    {
        ImGui.BeginChild(1);

        if (ImGui.BeginTable("Guests", 6, ImGuiTableFlags.Sortable))
        {
            ImGui.TableSetupColumn("Latest Entry", ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableSetupColumn("Name");
            ImGui.TableSetupColumn("Entries", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Minutes", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("World");
            ImGui.TableHeadersRow();
            
            ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();
            var sortedGuestList = GetSortedGuests(sortSpecs);

            foreach (var player in sortedGuestList)
            {
                var playerColor = VenueColors.StaffName;
                var color = VenueColors.StaffName;

                ImGui.TableNextColumn();
                ImGui.TextColored(color, player.Value.LatestEntry.ToString("h:mm tt"));
                ImGui.TableNextColumn();
                ImGui.TextColored(playerColor, player.Value.Name);
                if (ImGui.IsItemClicked()) {
                    _chatService.ChatPlayerLink(player.Value);
                }
                ImGui.TableNextColumn();
                ImGui.TextColored(color, "" + player.Value.EntryCount);
                ImGui.TableNextColumn();
                ImGui.TextColored(color, $"{player.Value.LastRoll}/{player.Value.LastRollMax}");
                ImGui.TableNextColumn();
                ImGui.TextColored(color, "" + player.Value.GetTimeInHouse());
                ImGui.TableNextColumn();
                ImGui.TextColored(color, player.Value.WorldName);
            }

            ImGui.EndTable();
        }
        
        ImGui.EndChild();
    }
}
