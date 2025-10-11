using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Services;
using VenueSync.Services;

namespace VenueSync.Ui;

public class VenueWindowPosition : IService
{
    public bool    IsOpen   { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size     { get; set; }
}

public class VenueWindow : Window, IDisposable
{
    private readonly Configuration _configuration;
    private readonly StateService _stateService;
    private readonly VenueWindowPosition _position;
    
    public VenueWindow(IDalamudPluginInterface pluginInterface, Configuration configuration, 
        StateService stateService, VenueWindowPosition position) : base("VenueSyncVenueWindow")
    {
        pluginInterface.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(800, 600),
            MaximumSize = new Vector2(800, 600),
        };
        _configuration = configuration;
        _stateService = stateService;
        _position = position;
    }
    
    public override void PreDraw()
    {
        _position.IsOpen = IsOpen;
        WindowName = $"Venue: {_stateService.VenueState.name}###VenueSyncVenueWindow";
    }

    public void Dispose()
    {
        _position.IsOpen = false;
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
        
        ImGui.TextUnformatted("Inside Venue! :D");
    }
}
