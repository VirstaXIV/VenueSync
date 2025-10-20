using OtterGui.Classes;
using VenueSync.State;

namespace VenueSync.Events;

public sealed class VenueEntered(): EventWrapper<VenueState, VenueEntered.Priority>(nameof(VenueEntered))
{
    public enum Priority
    {
        High = 1,
    }
}
