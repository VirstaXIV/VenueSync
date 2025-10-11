using System;
using Dalamud.Plugin.Services;

namespace VenueSync.Services;

public class PlayerWatcher: IDisposable
{
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly StateService _stateService;
    
    private bool _running = false;
    
    public PlayerWatcher(IFramework framework, IClientState clientState, StateService stateService)
    {
        _framework = framework;
        _clientState = clientState;
        _stateService = stateService;
        
        _framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_running) {
            VenueSync.Log.Warning("Skipping processing player while already running.");
            return;
        }
        _running = true;

        try
        {
            SetCurrentPlayer();
        }
        catch (Exception)
        {
            VenueSync.Log.Error("VenueSync Failed during framework update (player)");
        }
        
        _running = false;
    }

    private void SetCurrentPlayer()
    {
        var player = _clientState.LocalPlayer;
        if (player == null)
        {
            return;
        }
        
        var name = player.Name.TextValue;
        var world = player.HomeWorld.Value.Name.ToString().ToLower();
        var worldId = player.HomeWorld.Value.RowId;
        var dataCenter = (
            VenueSync.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>()?.GetRow(worldId).DataCenter.Value.Name.ToString() ?? ""
        ).ToLower();

        if (_stateService.PlayerState.name == name && _stateService.PlayerState.world == world && _stateService.PlayerState.data_center == dataCenter)
        {
            return;
        }
        
        VenueSync.Log.Debug($"Setting current player to {name} - {world} [{dataCenter}]");
        _stateService.PlayerState.name = name;
        _stateService.PlayerState.world = world;
        _stateService.PlayerState.data_center = dataCenter;
    }
}
