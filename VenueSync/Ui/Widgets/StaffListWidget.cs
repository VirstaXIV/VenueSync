using Dalamud.Bindings.ImGui;
using OtterGui.Services;
using VenueSync.Services;

namespace VenueSync.Ui.Widgets;

public class StaffListWidget: IService
{
    private readonly ChatService _chatService;
    private readonly StateService _stateService;
    
    public StaffListWidget(ChatService chatService, StateService stateService)
    {
        _chatService = chatService;
        _stateService = stateService;
    }
    
    public void Draw()
    {
        ImGui.TextColored(VenueColors.ActiveIndicator, "Inside");
        ImGui.Separator();
        ImGui.Spacing();
        foreach (var staff in _stateService.VenueState.staff)
        {
            if (_stateService.VisitorsState.players.ContainsKey(staff.name))
            {
                ImGui.PushID(staff.name);
                ImGui.TextColored(VenueColors.StaffName, staff.name);
                ImGui.SameLine();
                ImGui.TextColored(VenueColors.StaffPosition, $"- {staff.position}");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.PopID();
            }
        }
        
        ImGui.TextColored(VenueColors.WarningText, "Away");
        ImGui.Separator();
        ImGui.Spacing();
        foreach (var staff in _stateService.VenueState.staff)
        {
            if (!_stateService.VisitorsState.players.ContainsKey(staff.name))
            {
                ImGui.PushID(staff.name);
                ImGui.TextColored(VenueColors.StaffName, staff.name);
                ImGui.SameLine();
                ImGui.TextColored(VenueColors.StaffPosition, $"- {staff.position}");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.PopID();
            }
        }
    }
}