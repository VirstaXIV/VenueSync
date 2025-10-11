using System;
using Dalamud.Interface.ImGuiNotification;
using OtterGui.Classes;
using VenueSync.Events;
using VenueSync.Ui;

namespace VenueSync.Services;

public class VenueService: IDisposable
{
    private readonly StateService _stateService;
    private readonly VenueWindow _venueWindow;
    private readonly VenueEntered _venueEntered;
    
    public VenueService(StateService stateService, VenueWindow venueWindow, VenueEntered @venueEntered)
    {
        _stateService = stateService;
        _venueWindow = venueWindow;
        _venueEntered = @venueEntered;
        
        _venueEntered.Subscribe(OnVenueEntered, VenueEntered.Priority.High);
    }

    public void Dispose()
    {
        _venueEntered.Unsubscribe(OnVenueEntered);
    }

    private void OnVenueEntered(VenueEnteredData data)
    {
        _stateService.VenueState.id = data.venue.id;
        _stateService.VenueState.name = data.venue.name;
        _stateService.VenueState.location = data.location;
        
        VenueSync.Messager.NotificationMessage($"Welcome to [{data.venue.name}]", NotificationType.Success);
        
        _venueWindow.Toggle();
    }
}
