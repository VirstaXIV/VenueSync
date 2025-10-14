using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using VenueSync.Events;

namespace VenueSync.Services;

public class GameStateService: IDisposable
{
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly StateService _stateService;
    private readonly ICondition _condition;
    
    private readonly LoggedIn _loggedInEvent;
    private readonly LoggedOut _loggedOutEvent;
    
    public bool IsAnythingDrawing { get; private set; } = false;
    public bool IsInCutscene { get; private set; } = false;
    public bool IsInGpose { get; private set; } = false;
    public bool IsLoggedIn { get; private set; }
    public bool IsInCombatOrPerforming { get; private set; } = false;
    
    public GameStateService(IFramework framework, IClientState clientState, ICondition condition, StateService stateService, LoggedIn @loggedIn, LoggedOut @loggedOut)
    {
        _framework = framework;
        _clientState = clientState;
        _stateService = stateService;
        _condition = condition;
        
        _loggedInEvent = @loggedIn;
        _loggedOutEvent = @loggedOut;
        
        VenueSync.Log.Debug($"Starting Client State Service.");
        Start();
    }

    private void Start()
    {
        _framework.Update += OnFrameworkUpdate;
    }

    private void SetCurrentPlayer()
    {
        VenueSync.Log.Debug("Setting current player");
        EnsureIsOnFramework();
        
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

    public void EnsureIsOnFramework()
    {
        if (!_framework.IsInFrameworkUpdateThread) throw new InvalidOperationException("Can only be run on Framework");
    }
    
    public async Task RunOnFrameworkThread(Action act, [CallerMemberName] string callerMember = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        var fileName = Path.GetFileNameWithoutExtension(callerFilePath);
        if (!_framework.IsInFrameworkUpdateThread)
        {
            await _framework.RunOnFrameworkThread(act).ContinueWith((_) => Task.CompletedTask).ConfigureAwait(false);
            while (_framework.IsInFrameworkUpdateThread) // yield the thread again, should technically never be triggered
            {
                await Task.Delay(1).ConfigureAwait(false);
            }
        }
        else
        {
            act();
        }
    }

    public async Task<T> RunOnFrameworkThread<T>(Func<T> func, [CallerMemberName] string callerMember = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
    {
        var fileName = Path.GetFileNameWithoutExtension(callerFilePath);
        if (!_framework.IsInFrameworkUpdateThread)
        {
            var result = await _framework.RunOnFrameworkThread(func).ContinueWith((task) => task.Result).ConfigureAwait(false);
            while (_framework.IsInFrameworkUpdateThread) // yield the thread again, should technically never be triggered
            {
                await Task.Delay(1).ConfigureAwait(false);
            }
            return result;
        }

        return func.Invoke();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            if (_clientState.LocalPlayer?.IsDead ?? false)
            {
                return;
            }
            
            if (_clientState.IsGPosing && !IsInGpose)
            {
                VenueSync.Log.Debug("Gpose start");
                IsInGpose = true;
            }
            else if (!_clientState.IsGPosing && IsInGpose)
            {
                VenueSync.Log.Debug("Gpose end");
                IsInGpose = false;
            }
            
            if ((_condition[ConditionFlag.Performing] || _condition[ConditionFlag.InCombat]) && !IsInCombatOrPerforming)
            {
                VenueSync.Log.Debug("Combat/Performance start");
                IsInCombatOrPerforming = true;
            }
            else if ((!_condition[ConditionFlag.Performing] && !_condition[ConditionFlag.InCombat]) && IsInCombatOrPerforming)
            {
                VenueSync.Log.Debug("Combat/Performance end");
                IsInCombatOrPerforming = false;
            }
            
            if (_condition[ConditionFlag.WatchingCutscene] && !IsInCutscene)
            {
                VenueSync.Log.Debug("Cutscene start");
                IsInCutscene = true;
            }
            else if (!_condition[ConditionFlag.WatchingCutscene] && IsInCutscene)
            {
                VenueSync.Log.Debug("Cutscene end");
                IsInCutscene = false;
            }
            
            var localPlayer = _clientState.LocalPlayer;
            if (localPlayer != null && !IsLoggedIn)
            {
                VenueSync.Log.Debug("Logged in");
                IsLoggedIn = true;
                SetCurrentPlayer();
                _loggedInEvent.Invoke();
            }
            else if (localPlayer == null && IsLoggedIn)
            {
                VenueSync.Log.Debug("Logged out");
                IsLoggedIn = false;
                _loggedOutEvent.Invoke();
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"VenueSync Failed during framework update (service): {exception.Message}");
        }
    }
    
    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
    }
}
