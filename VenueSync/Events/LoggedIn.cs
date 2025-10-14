using OtterGui.Classes;

namespace VenueSync.Events;

public sealed class LoggedIn(): EventWrapper<LoggedIn.Priority>(nameof(LoggedIn))
{
    public enum Priority
    {
        None = 0,
        High = 1,
    }
}
