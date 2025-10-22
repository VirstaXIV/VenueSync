using System;
using VenueSync.State;

namespace VenueSync.Services;

public class StateService: IDisposable
{
    public required Connection Connection { get; set; }
    public required UserState UserState { get; set; }
    public required PlayerState PlayerState { get; set; }
    public required VenueState VenueState { get; set; }
    public required House CurrentHouse { get; set; }
    public required Mannequin ActiveMannequin { get; set; }
    public required VisitorsState VisitorsState { get; set; }
    public required ModsState ModsState { get; set; }
    
    public StateService()
    {
        ResetState();
    }

    public void Dispose()
    {
        ResetState();
    }

    public bool HasCharacters()
    {
        return UserState.characters.Count > 0;
    }
    
    public bool HasVenues()
    {
        return UserState.venues.Count > 0;
    }
    
    public bool HasHouses()
    {
        return UserState.houses.Count > 0;
    }
    
    public bool HasMods()
    {
        return ModsState.modList.Count > 0;
    }

    public void ResetVenueState()
    {
        VenueState = new VenueState() {
            location = new VenueLocation() {
                mannequins = [],
            }
        };
    }
    
    public void ResetHouseState()
    {
        CurrentHouse = new House();
    }

    public void ResetModsState()
    {
        ModsState = new ModsState() {
            modList = []
        };
    }

    private void ResetState()
    {
        Connection = new Connection();
        UserState = new UserState()
        {
            venues = [],
            characters = [],
            houses = []
        };
        PlayerState = new PlayerState();
        ResetVenueState();
        ResetHouseState();
        ActiveMannequin = new Mannequin();
        VisitorsState = new VisitorsState();
        ResetModsState();
    }
}
