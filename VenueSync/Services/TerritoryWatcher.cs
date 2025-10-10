using System;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace VenueSync.Services;

public class House
{
    public long HouseId {get; set;} = 0;
    public int Plot {get; set;} = 0;
    public int Ward {get; set;} = 0;
    public int Room {get; set;} = 0;
    public string District {get; set;} = "";
    public uint? WorldId {get; set;} = 0;
    public string Type {get; set;} = "";
    public string WorldName {get; set;} = "";
    public string DataCenter {get; set;} = "";
}

public class TerritoryWatcher: IDisposable
{
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly LocationService _locationService;

    private bool _isInHouse = false;
    private bool _wasInHouse = false;
    public ushort CurrentTerritory;
    private House _activeHouse = new House();
    private bool _running = false;
    
    public TerritoryWatcher(IFramework framework, IClientState clientState, LocationService locationService)
    {
        _framework = framework;
        _clientState = clientState;
        _locationService = locationService;
        
        _framework.Update += OnFrameworkUpdate;
        _clientState.TerritoryChanged += OnTerritoryChanged;
        _clientState.Logout += OnLogout;
        
        OnTerritoryChanged(_clientState.TerritoryType);
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
    
    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
        _clientState.TerritoryChanged -= OnTerritoryChanged;
        _clientState.Logout -= OnLogout;
        CurrentTerritory = 0;
        LeftHouse();
    }
    
    private void OnLogout(int type, int code)
    {
        CurrentTerritory = 0;
        LeftHouse();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_running) {
            VenueSync.Log.Warning("Skipping processing while already running.");
            return;
        }
        _running = true;

        try
        {
            if (_wasInHouse)
            {
                try
                {
                    SetCurrentHouse();
                }
                catch
                {
                    VenueSync.Log.Error("VenueSync Failed to check house");
                }
            }
        }
        catch (Exception e)
        {
            VenueSync.Log.Error("VenueSync Failed during framework update");
            VenueSync.Log.Error(e.ToString());
        }
        
        _running = false;
    }

    private unsafe void OnTerritoryChanged(ushort territory)
    {
        CurrentTerritory = territory;
        
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
            throw;
        }
    }

    private void SendLocation()
    {
        _ = Task.Run(async () =>
        {
            var reply = await _locationService.SendLocation(_activeHouse);
            if (reply is { Success: false, Graceful: false })
            {
                VenueSync.Log.Warning("Failed to query location.");
            }
        });
    }

    private unsafe void SetCurrentHouse()
    {
        var housingManager = HousingManager.Instance();
        var worldId = _clientState.LocalPlayer?.CurrentWorld.Value.RowId!;
        
        if (_activeHouse.HouseId != (long)housingManager->GetCurrentIndoorHouseId().Id && worldId != null)
        {
            _activeHouse = new House {
                HouseId = (long)housingManager->GetCurrentIndoorHouseId().Id,
                District = GetDistrict((long)housingManager->GetCurrentIndoorHouseId().Id),
                Type = GetHouseType((ushort)HousingManager.GetOriginalHouseTerritoryTypeId()),
                Plot = housingManager->GetCurrentPlot() + 1,
                Ward = housingManager->GetCurrentWard() + 1,
                Room = housingManager->GetCurrentRoom(),
                WorldId = worldId,
                WorldName = GetWorldName(worldId),
                DataCenter = GetDataCenter(worldId)
            };

            var address = $"{_activeHouse.District} {_activeHouse.Ward}/{_activeHouse.Plot} {_activeHouse.WorldName} [{_activeHouse.DataCenter}]";

            if (_activeHouse.Room != 0)
            {
                address = $"{_activeHouse.District} {_activeHouse.Ward}/{_activeHouse.Plot} (Room: {_activeHouse.Room}) {_activeHouse.WorldName} [{_activeHouse.DataCenter}]";
            }
            
            VenueSync.Log.Information($"Set house location to {address}");
            
            SendLocation();
        }
    }

    private void EnteredHouse()
    {
        _wasInHouse = true;
        VenueSync.Log.Information("Player Entered House");
    }
    
    private void LeftHouse()
    {
        _wasInHouse = false;
        _activeHouse = new House();
        VenueSync.Log.Information("Player Left House");
    }
    
    public static string GetHouseType(ushort territory)
    {
        if (SmallHouseTypes.Contains(territory)) return "small";
        if (MediumHouseTypes.Contains(territory)) return "medium";
        if (LargeHouseTypes.Contains(territory)) return "large";
        if (ChamberTypes.Contains(territory)) return "chamber";
        if (AppartmentTypes.Contains(territory)) return "apartment";
        return "unknown";
    }

    private string GetDataCenter(uint? worldId)
    {
        if (worldId != null)
        {
            uint rowId = (uint)worldId;
            return VenueSync.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>()?.GetRow(rowId).DataCenter.Value.Name.ToString() ?? "";
        }
        return "";
    }

    private string GetWorldName(uint? worldId)
    {
        if (worldId != null)
        {
            uint rowId = (uint)worldId;
            return VenueSync.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>()?.GetRow(rowId).Name.ToString() ?? $"World_{worldId}";
        }
        return "";
    }

    private string GetDistrict (long houseId) {
        uint territoryId = (uint)((houseId >> 32) & 0xFFFF);
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
