using OtterGui.Classes;

namespace VenueSync.Events;

public sealed class ServiceConnected(): EventWrapper<ServiceConnected.Priority>(nameof(ServiceConnected))
{
    public enum Priority
    {
        None = 0,
        High = 1,
    }
}
