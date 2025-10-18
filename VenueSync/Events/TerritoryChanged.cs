using OtterGui.Classes;

namespace VenueSync.Events;

public sealed class TerritoryChanged(): EventWrapper<TerritoryChanged.Priority>(nameof(TerritoryChanged))
{
    public enum Priority
    {
        None = 0,
        High = 1,
    }
}
