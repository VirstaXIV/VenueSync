using OtterGui.Classes;

namespace VenueSync.Events;

public sealed class ServiceDisconnected(): EventWrapper<ServiceDisconnected.Priority>(nameof(ServiceDisconnected))
{
    public enum Priority
    {
        None = 0,
        High = 1,
    }
}
