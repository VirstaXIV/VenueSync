using OtterGui.Classes;
using VenueSync.State;

namespace VenueSync.Events;

public sealed class VenueUpdated(): EventWrapper<VenueState, VenueUpdated.Priority>(nameof(VenueUpdated))
{
    public enum Priority
    {
        High = 1,
    }
}
