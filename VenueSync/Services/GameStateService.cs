using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Text;
using FFXIVClientStructs.STD;
using VenueSync.Events;

namespace VenueSync.Services;

public class GameStateService: IDisposable
{
    [Signature("E8 ?? ?? ?? ?? EB ?? 45 33 C9 4C 8B C6", DetourName = nameof(RandomPrintLogDetour))]
    private Hook<RandomPrintLogDelegate>? RandomPrintLogHook { get; set; }
    private unsafe delegate void RandomPrintLogDelegate(RaptureLogModule* module, int logMessageId, byte* playerName, byte sex, StdDeque<TextParameter>* parameter, byte flags, ushort homeWorldId);

    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 55 57 41 54 41 55 41 56 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 0F B7 75", DetourName = nameof(DicePrintLogDetour))]
    private Hook<DicePrintLogDelegate>? DicePrintLogHook { get; set; }
    private unsafe delegate void DicePrintLogDelegate(RaptureLogModule* module, ushort chatType, byte* userName, void* unused, ushort worldId, ulong accountId, ulong contentId, ushort roll, ushort outOf, uint entityId, byte ident);
        
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly StateService _stateService;
    private readonly ICondition _condition;
    private readonly IObjectTable _objectTable;
    
    private readonly LoggedIn _loggedInEvent;
    private readonly LoggedOut _loggedOutEvent;
    private readonly DiceRoll _diceRoll;
    
    public bool IsAnythingDrawing { get; private set; } = false;
    public bool IsInCutscene { get; private set; } = false;
    public bool IsInGpose { get; private set; } = false;
    public bool IsLoggedIn { get; private set; }
    public bool IsInCombatOrPerforming { get; private set; } = false;
    
    public GameStateService(IFramework framework, IClientState clientState, ICondition condition, IObjectTable objectTable, StateService stateService, 
        LoggedIn @loggedIn, LoggedOut @loggedOut, DiceRoll @diceRoll)
    {
        _framework = framework;
        _clientState = clientState;
        _stateService = stateService;
        _condition = condition;
        _objectTable = objectTable;
        
        _loggedInEvent = @loggedIn;
        _loggedOutEvent = @loggedOut;
        _diceRoll = @diceRoll;
        
        VenueSync.Log.Debug($"Starting Client State Service.");
        StartFramework();
        StartLoginState();
        
        RandomPrintLogHook?.Enable();
        DicePrintLogHook?.Enable();
    }

    private void StartFramework()
    {
        _framework.Update += OnFrameworkUpdate;
    }

    private void StartLoginState()
    {
        _clientState.Login += OnLogin;
        _clientState.Logout += OnLogout;
    }

    private void OnLogin()
    {
        VenueSync.Log.Debug("Client Login.");
        _loggedInEvent.Invoke();
    }

    private void OnLogout(int type, int code)
    {
        VenueSync.Log.Debug("Client Logout.");
        _loggedOutEvent.Invoke();
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
                // This will trigger on changing zones too.
                VenueSync.Log.Debug("Logged in");
                IsLoggedIn = true;
                SetCurrentPlayer();
            }
            else if (localPlayer == null && IsLoggedIn)
            {
                // This will trigger on changing zones too.
                VenueSync.Log.Debug("Logged out");
                IsLoggedIn = false;
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"VenueSync Failed during framework update (service): {exception.Message}");
        }
    }
    
    public void Dispose()
    {
        RandomPrintLogHook?.Dispose();
        DicePrintLogHook?.Dispose();
        _framework.Update -= OnFrameworkUpdate;
        _clientState.Login -= OnLogin;
        _clientState.Logout -= OnLogout;
    }
    
    private unsafe void RandomPrintLogDetour(RaptureLogModule* module, int logMessageId, byte* playerName, byte sex, StdDeque<TextParameter>* parameter, byte flags, ushort homeWorldId)
    {
        if (logMessageId != 856 && logMessageId != 3887)
        {
            RandomPrintLogHook!.Original(module, logMessageId, playerName, sex, parameter, flags, homeWorldId);
            return;
        }

        try
        {
            var name = MemoryHelper.ReadStringNullTerminated((nint)playerName);
            var roll = (*parameter)[1].IntValue;
            var outOf = logMessageId == 3887 ? (*parameter)[2].IntValue : 0;

            _diceRoll.Invoke(new DiceRollData() {
                name = name, 
                homeWorldId = homeWorldId, 
                roll = roll, 
                outOf = outOf
            });
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Unable to process random roll: {exception.Message}");
        }

        RandomPrintLogHook!.Original(module, logMessageId, playerName, sex, parameter, flags, homeWorldId);
    }

    private unsafe void DicePrintLogDetour(RaptureLogModule* module, ushort chatType, byte* playerName, void* unused, ushort worldId, ulong accountId, ulong contentId, ushort roll, ushort outOf, uint entityId, byte ident)
    {
        try
        {
            var name = MemoryHelper.ReadStringNullTerminated((nint)playerName);
            _diceRoll.Invoke(new DiceRollData() {
                name = name, 
                homeWorldId = worldId, 
                roll = roll, 
                outOf = outOf
            });
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Unable to process dice roll: {exception.Message}");
        }

        DicePrintLogHook!.Original(module, chatType, playerName, unused, worldId, accountId, contentId, roll, outOf, entityId, ident);
    }
}
