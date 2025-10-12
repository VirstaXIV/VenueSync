using OtterGui.Classes;
using VenueSync.Services;

namespace VenueSync.Events;

public sealed class VenueExited(): EventWrapper<VenueExited.Priority>(nameof(VenueExited))
{
    public enum Priority
    {
        /// <seealso cref="VenueService.OnVenueExited"/>
        High = 1,
    }
}
