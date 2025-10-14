using OtterGui.Classes;

namespace VenueSync.Events;

public sealed class LoggedOut(): EventWrapper<LoggedOut.Priority>(nameof(LoggedOut))
{
    public enum Priority
    {
        None = 0,
        High = 1,
    }
}
