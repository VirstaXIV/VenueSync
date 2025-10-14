using OtterGui.Classes;

namespace VenueSync.Events;

public sealed class ReloadMods(): EventWrapper<ReloadMods.Priority>(nameof(ReloadMods))
{
    public enum Priority
    {
        None = 0,
        High = 1,
    }
}