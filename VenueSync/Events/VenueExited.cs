using OtterGui.Classes;
using VenueSync.Services;

namespace VenueSync.Events;

public sealed class VenueExited(): EventWrapper<string, VenueExited.Priority>(nameof(VenueExited))
{
    public enum Priority
    {
        High = 1,
    }
}
