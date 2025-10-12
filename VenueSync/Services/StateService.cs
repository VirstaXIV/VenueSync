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
        VenueState = new VenueState() {
            location = new VenueLocation()
        };
        CurrentHouse = new House();
        ActiveMannequin = new Mannequin();
    }
}
