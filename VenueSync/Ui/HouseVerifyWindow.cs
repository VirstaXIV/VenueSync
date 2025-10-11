using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Raii;
using OtterGui.Services;
using VenueSync.Services;

namespace VenueSync.Ui;

public class HouseVerifyWindowPosition : IService
{
    public bool    IsOpen   { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size     { get; set; }
}

public class HouseVerifyWindow : Window, IDisposable
{
    private readonly Configuration _configuration;
    private readonly StateService _stateService;
    private readonly LocationService _locationService;
    private readonly HouseVerifyWindowPosition _position;
    
    public long _ownedHouseId = 0;
    public int _ownedHousePlot = 0;
    public int _ownedHouseWard = 0;
    
    public HouseVerifyWindow(IDalamudPluginInterface pluginInterface, Configuration configuration, 
        StateService stateService, HouseVerifyWindowPosition position, LocationService locationService) : base("VenueSyncHouseVerifyWindow")
    {
        pluginInterface.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(400, 350),
            MaximumSize = new Vector2(400, 350),
        };
        _configuration = configuration;
        _stateService = stateService;
        _locationService = locationService;
        _position = position;
    }
    
    public override void PreDraw()
    {
        _position.IsOpen = IsOpen;
        WindowName = $"Verify House Ownership###VenueSyncHouseVerifyWindow";
    }

    public void Dispose()
    {
        _position.IsOpen = false;
        _ownedHouseId = 0;
        _ownedHousePlot = 0;
        _ownedHouseWard = 0;
    }

    public override void Draw()
    {
        _position.Size = ImGui.GetWindowSize();
        _position.Position = ImGui.GetWindowPos();

        if (!_stateService.Connection.Connected)
        {
            ImGui.TextUnformatted("Not Connected");
            return;
        }

        if (_ownedHouseId == 0)
        {
            ImGui.TextUnformatted("Sorry, can't find an owned house on this character.");
        }

        using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey))
        {
            ImGui.PushTextWrapPos(0);
            ImGui.TextUnformatted("Note: Verification will involve manual review. " +
                                  "Once submitted, a moderator will manually check your placard. " +
                                  "House verification is meant for venues, so not everyone will need it.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        
        ImGui.TextUnformatted("Here is the known details on your Personal House.");
        
        var characterName = _stateService.PlayerState.name;
        var lodestoneId = string.Empty;

        foreach (var character in _stateService.UserState.characters)
        {
            if (characterName == character.name)
            {
                lodestoneId = character.lodestone_id;
                break;
            }
        }

        var foundPlot = _ownedHousePlot.ToString();
        var foundWard = _ownedHouseWard.ToString();
        var foundWorld = _stateService.PlayerState.world;
        var foundDatacenter = _stateService.PlayerState.data_center;
        
        ImGui.InputText("Character Name", ref characterName, 255, ImGuiInputTextFlags.ReadOnly);
        ImGui.InputText("Lodestone ID", ref lodestoneId, 255, ImGuiInputTextFlags.ReadOnly);
        ImGui.InputText("Plot", ref foundPlot, 255, ImGuiInputTextFlags.ReadOnly);
        ImGui.InputText("Ward", ref foundWard, 255, ImGuiInputTextFlags.ReadOnly);
        
        ImGui.InputText("World", ref foundWorld, 255, ImGuiInputTextFlags.ReadOnly);
        ImGui.InputText("Datacenter", ref foundDatacenter, 255, ImGuiInputTextFlags.ReadOnly);
        
        ImGui.Spacing();
        if (_ownedHouseId == _stateService.CurrentHouse.HouseId)
        {
            if (ImGui.Button("Verify"))
            {
                VenueSync.Log.Debug("Verifying house");
                _ = Task.Run(async () =>
                {
                    var reply = await _locationService.VerifyLocation(characterName, lodestoneId, _stateService.CurrentHouse);
                    if (reply is { Success: false, Graceful: false })
                    {
                        VenueSync.Log.Warning("Failed to verify location.");
                    }
                    else
                    {
                        VenueSync.Log.Debug("House verified.");
                    }
                });
            }
        }
        else
        {
            ImGui.TextUnformatted("To Verify, enter your house.");
        }
    }
}