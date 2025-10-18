using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using VenueSync.Data;
using VenueSync.Events;
using VenueSync.State;
using VenueSync.Ui;

namespace VenueSync.Services;

public class TerritoryWatcher: IDisposable
{
    private readonly StateService _stateService;
    private readonly IObjectTable _objectTable;
    private readonly LocationService _locationService;
    private readonly HouseVerifyWindow _houseVerifyWindow;
    private readonly ChatService _chatService;
    private readonly Configuration _configuration;
    
    private readonly ServiceConnected _serviceConnected;
    private readonly VenueExited _venueExited;
    private readonly TerritoryChanged _territoryChanged;
    private readonly DiceRoll _diceRoll;
    private readonly LoggedOut _loggedOut;

    private bool _isInHouse;
    private bool _wasInHouse;
    private bool _justEntered;
    
    public TerritoryWatcher(
        StateService stateService,
        IObjectTable objectTable,
        LocationService locationService,
        HouseVerifyWindow houseVerifyWindow,
        ChatService chatService,
        Configuration configuration,
        ServiceConnected serviceConnected,
        VenueExited venueExited,
        TerritoryChanged territoryChanged,
        LoggedOut loggedOut,
        DiceRoll diceRoll)
    {
        _stateService = stateService;
        _objectTable = objectTable;
        _locationService = locationService;
        _houseVerifyWindow = houseVerifyWindow;
        _chatService = chatService;
        _configuration = configuration;
        _serviceConnected = serviceConnected;
        _venueExited = venueExited;
        _territoryChanged = territoryChanged;
        _diceRoll = diceRoll;
        _loggedOut = loggedOut;

        _territoryChanged.Subscribe(OnTerritoryChanged, TerritoryChanged.Priority.High);
        _serviceConnected.Subscribe(OnConnection, ServiceConnected.Priority.High);
        _diceRoll.Subscribe(OnDiceRoll, DiceRoll.Priority.High);
        _loggedOut.Subscribe(OnLogout, LoggedOut.Priority.High);
    }

    public void Dispose()
    {
        _territoryChanged.Unsubscribe(OnTerritoryChanged);
        _serviceConnected.Unsubscribe(OnConnection);
        _diceRoll.Unsubscribe(OnDiceRoll);
        _loggedOut.Unsubscribe(OnLogout);
        LeftHouse();
    }
    
    // Mist locations 
    private static readonly ushort MIST_SMALL = 282;
    private static readonly ushort MIST_MEDIUM = 283;
    private static readonly ushort MIST_LARGE = 284;
    private static readonly ushort MIST_CHAMBER = 384;
    private static readonly ushort MIST_APARTMENT = 608;

    // The Lavender Beds locations 
    private static readonly ushort LAVENDER_SMALL = 342;
    private static readonly ushort LAVENDER_MEDIUM = 343;
    private static readonly ushort LAVENDER_LARGE = 344;
    private static readonly ushort LAVENDER_CHAMBER = 385;
    private static readonly ushort LAVENDER_APARTMENT = 609;

    // The Goblet
    private static readonly ushort GOBLET_SMALL = 345;
    private static readonly ushort GOBLET_MEDIUM = 346;
    private static readonly ushort GOBLET_LARGE = 347;
    private static readonly ushort GOBLET_LARGE_2 = 1251;
    private static readonly ushort GOBLET_CHAMBER = 386;
    private static readonly ushort GOBLET_APARTMENT = 610;

    // Shirogane 
    private static readonly ushort SHIROGANE_SMALL = 649;
    private static readonly ushort SHIROGANE_MEDIUM = 650;
    private static readonly ushort SHIROGANE_LARGE = 651;
    private static readonly ushort SHIROGANE_CHAMBER = 652;
    private static readonly ushort SHIROGANE_APARTMENT = 655;

    // Empyreum 
    private static readonly ushort EMPYREUM_SMALL = 980;
    private static readonly ushort EMPYREUM_MEDIUM = 981;
    private static readonly ushort EMPYREUM_LARGE = 982;
    private static readonly ushort EMPYREUM_CHAMBER = 983;
    private static readonly ushort EMPYREUM_APARTMENT = 999;
    
    private static readonly ushort[] SmallHouseTypes = [
        MIST_SMALL, LAVENDER_SMALL, GOBLET_SMALL, SHIROGANE_SMALL, EMPYREUM_SMALL
    ];

    private static readonly ushort[] MediumHouseTypes = [
        MIST_MEDIUM, LAVENDER_MEDIUM, GOBLET_MEDIUM, SHIROGANE_MEDIUM, EMPYREUM_MEDIUM
    ];

    private static readonly ushort[] LargeHouseTypes = [
        MIST_LARGE, LAVENDER_LARGE, GOBLET_LARGE, GOBLET_LARGE_2, SHIROGANE_LARGE, EMPYREUM_LARGE
    ];

    private static readonly ushort[] ChamberTypes = [
        MIST_CHAMBER, LAVENDER_CHAMBER, GOBLET_CHAMBER, SHIROGANE_CHAMBER, EMPYREUM_CHAMBER
    ];

    private static readonly ushort[] AppartmentTypes = [
        MIST_APARTMENT, LAVENDER_APARTMENT, GOBLET_APARTMENT, SHIROGANE_APARTMENT, EMPYREUM_APARTMENT
    ];
    
    private void OnLogout()
    {
        _houseVerifyWindow._ownedHouseId = 0;
        _houseVerifyWindow._ownedHousePlot = 0;
        _houseVerifyWindow._ownedHouseWard = 0;
        LeftHouse();
    }
    
    public void OnFrameworkUpdate()
    {
        try
        {
            if (_houseVerifyWindow.IsOpen)
            {
                CheckOwnedHouse();
            }
            if (!_wasInHouse)
            {
                return;
            }
            try
            {
                SetCurrentHouse();
                CheckSeenPlayers();
            }
            catch
            {
                VenueSync.Log.Error("VenueSync Failed to check house");
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"VenueSync Failed during framework update (territory): {exception.Message}");
        }
    }

    private void CheckSeenPlayers()
    {
        if (_stateService.VenueState.id == string.Empty)
        {
            return;
        }
        var playerCount = 0;
        Dictionary<string, bool> seenPlayers = new();
        foreach (var o in _objectTable)
        {
            if (o is not IPlayerCharacter pc) continue;
            if (pc.Name.TextValue.Length == 0) continue;
            if (o.SubKind != 4) continue;
            var player = Player.FromCharacter(pc);

            playerCount++;

            seenPlayers.Add(player.Name, true);

            var isSelf = _stateService.PlayerState.name == player.Name;
            if (_stateService.VisitorsState.players.TryAdd(player.Name, player))
            {
                if (_configuration is { NotifyEntrances: true, ClientMode: false })
                {
                    _chatService.ChatPlayerLink(player, " has come inside.");
                }
            }
            else if (!_stateService.VisitorsState.players[player.Name].InHouse)
            {
                _stateService.VisitorsState.players[player.Name].InHouse = true;
                _stateService.VisitorsState.players[player.Name].LatestEntry = DateTime.Now;
                _stateService.VisitorsState.players[player.Name].TimeCursor = DateTime.Now;
                _stateService.VisitorsState.players[player.Name].EntryCount++;
                if (_configuration is { NotifyEntrances: true, ClientMode: false })
                {
                    _chatService.ChatPlayerLink(player, " has come inside.");
                }
            }
            else if (_justEntered)
            {
                _stateService.VisitorsState.players[player.Name].TimeCursor = DateTime.Now;
            }

            _stateService.VisitorsState.players[player.Name].LastSeen = DateTime.Now;

            if (_justEntered && isSelf)
            {
                _stateService.VisitorsState.players[player.Name].LatestEntry = DateTime.Now;
            }
        }

        foreach (var guest in _stateService.VisitorsState.players.Where(guest => guest.Value.InHouse))
        {
            if (!seenPlayers.ContainsKey(guest.Value.Name))
            {
                guest.Value.OnLeaveHouse();
            }
            else
            {
                guest.Value.OnAccumulateTime();
            }
        }

        _stateService.VisitorsState.count = playerCount;
        _justEntered = false;
    }

    private unsafe void OnTerritoryChanged()
    {
        try
        {
            var housingManager = HousingManager.Instance();
            _isInHouse = housingManager->IsInside();
            
            if (_isInHouse)
            {
                EnteredHouse();
            } else if (_wasInHouse)
            {
                LeftHouse();
            }
        }
        catch (Exception ex) 
        {
            VenueSync.Log.Warning("Could not get housing state on territory change. " + ex.Message);
        }
    }

    private void OnConnection()
    {
        if (_stateService.CurrentHouse.HouseId > 0)
        {
            SendLocation();
        }
    }

    private void OnDiceRoll(DiceRollData roll)
    {
        if (!_stateService.VisitorsState.players.TryGetValue(roll.name, out var value) || !value.InHouse)
        {
            return;
        }
        value.LastRoll = roll.roll;
        value.LastRollMax = roll.outOf == 0 ? 1000 : roll.outOf;
    }

    private void SendLocation()
    {
        _ = Task.Run(async () =>
        {
            if (_stateService.Connection.Connected)
            {
                var reply = await _locationService.SendLocation(_stateService.CurrentHouse);
                if (reply is { Success: false, Graceful: false })
                {
                    VenueSync.Log.Warning("Failed to query location.");
                }
            }
        });
    }

    private void CheckOwnedHouse()
    {
        var ownedHouse = HousingManager.GetOwnedHouseId(EstateType.PersonalEstate);
        if (_houseVerifyWindow._ownedHouseId == (long)ownedHouse.Id || ownedHouse is not { IsApartment: false, IsWorkshop: false })
        {
            return;
        }
        //Valid house?
        var plot = ownedHouse.PlotIndex + 1;
        var ward = ownedHouse.WardIndex + 1;
        _houseVerifyWindow._ownedHouseId = (long)ownedHouse.Id;
        _houseVerifyWindow._ownedHousePlot = plot;
        _houseVerifyWindow._ownedHouseWard = ward;
        VenueSync.Log.Debug($"Checking House: ID: {ownedHouse.Id} P: {plot} W: {ward}");
    }

    private unsafe void SetCurrentHouse()
    {
        var housingManager = HousingManager.Instance();
        if (_stateService.PlayerState.world_id == 0 || _stateService.CurrentHouse.HouseId == (long)housingManager->GetCurrentIndoorHouseId().Id)
        {
            return;
        }
        _stateService.CurrentHouse = new House() {
            HouseId = (long)housingManager->GetCurrentIndoorHouseId().Id,
            District = GetDistrict((long)housingManager->GetCurrentIndoorHouseId().Id),
            Type = GetHouseType((ushort)HousingManager.GetOriginalHouseTerritoryTypeId()),
            Plot = housingManager->GetCurrentPlot() + 1,
            Ward = housingManager->GetCurrentWard() + 1,
            Room = housingManager->GetCurrentRoom(),
            WorldId = _stateService.PlayerState.world_id,
            WorldName = GetWorldName(_stateService.PlayerState.world_id).ToLower(),
            DataCenter = GetDataCenter(_stateService.PlayerState.world_id).ToLower()
        };

        var address = $"{_stateService.CurrentHouse.District} {_stateService.CurrentHouse.Ward}/{_stateService.CurrentHouse.Plot} {_stateService.CurrentHouse.WorldName} [{_stateService.CurrentHouse.DataCenter}]";

        if (_stateService.CurrentHouse.Room != 0)
        {
            address = $"{_stateService.CurrentHouse.District} {_stateService.CurrentHouse.Ward}/{_stateService.CurrentHouse.Plot} (Room: {_stateService.CurrentHouse.Room}) {_stateService.CurrentHouse.WorldName} [{_stateService.CurrentHouse.DataCenter}]";
        }
            
        VenueSync.Log.Debug($"Set house location to {address}");
            
        SendLocation();
    }

    private void EnteredHouse()
    {
        _wasInHouse = true;
        _justEntered = true;
        VenueSync.Log.Debug("Player Entered House");
    }
    
    private void LeftHouse()
    {
        if (_stateService.VenueState.id != string.Empty)
        {
            _venueExited.Invoke(_stateService.VenueState.id);
        }
        _wasInHouse = false;
        _stateService.CurrentHouse = new House();
        _stateService.ResetVenueState();
        _stateService.VisitorsState = new VisitorsState();
        VenueSync.Log.Debug("Player Left House");
    }

    private static string GetHouseType(ushort territory)
    {
        if (SmallHouseTypes.Contains(territory)) return "small";
        if (MediumHouseTypes.Contains(territory)) return "medium";
        if (LargeHouseTypes.Contains(territory)) return "large";
        if (ChamberTypes.Contains(territory)) return "chamber";
        return AppartmentTypes.Contains(territory) ? "apartment" : "unknown";
    }

    private static string GetDataCenter(uint worldId)
    {
        return VenueSync.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRow(worldId).DataCenter.Value.Name.ToString();
    }

    private static string GetWorldName(uint worldId)
    {
        return VenueSync.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRow(worldId).Name.ToString();
    }

    private static string GetDistrict (long houseId) {
        var territoryId = (uint)((houseId >> 32) & 0xFFFF);
        var district = VenueSync.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRow(territoryId).PlaceNameZone.RowId;

        return district switch
        {
            502 => "mist",
            505 => "goblet",
            507 => "the_lavender_beds",
            512 => "empyreum",
            513 => "shirogane",
            _ => ""
        };
    }
}
