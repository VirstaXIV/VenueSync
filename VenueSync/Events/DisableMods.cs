using OtterGui.Classes;

namespace VenueSync.Events;

public sealed class DisableMods(): EventWrapper<DisableMods.Priority>(nameof(DisableMods))
{
    public enum Priority
    {
        None = 0,
        High = 1,
    }
}