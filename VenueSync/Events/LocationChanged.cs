using OtterGui.Classes;

namespace VenueSync.Events;

public sealed class LocationChanged(): EventWrapper<LocationChanged.Priority>(nameof(LocationChanged))
{
    public enum Priority
    {
        None = 0,
        High = 1,
    }
}