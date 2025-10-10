using OtterGui.Classes;
using VenueSync.Services;

namespace VenueSync.Events;

public class VenueData
{
    public string name { get; set; } = "";
}

public class VenueEnteredData
{
    public required VenueData venue { get; set; }
}

public sealed class VenueEntered(): EventWrapper<VenueEnteredData, VenueEntered.Priority>(nameof(VenueEntered))
{
    public enum Priority
    {
        /// <seealso cref="VenueService.OnVenueEntered"/>
        High = 1,
    }
}
