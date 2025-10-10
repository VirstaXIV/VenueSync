using System;
using Dalamud.Interface.ImGuiNotification;
using OtterGui.Classes;
using VenueSync.Events;

namespace VenueSync.Services;

public class VenueService: IDisposable
{
    private readonly VenueEntered _venueEntered;
    
    public VenueService(VenueEntered @venueEntered)
    {
        _venueEntered = @venueEntered;
        
        _venueEntered.Subscribe(OnVenueEntered, VenueEntered.Priority.High);
    }

    public void Dispose()
    {
        _venueEntered.Unsubscribe(OnVenueEntered);
    }

    private void OnVenueEntered(VenueEnteredData data)
    {
        VenueSync.Messager.NotificationMessage($"Welcome to [{data.venue.name}]", NotificationType.Success);
    }
}
