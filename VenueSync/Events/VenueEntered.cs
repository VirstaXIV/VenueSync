using OtterGui.Classes;
using VenueSync.Services;
using VenueSync.State;

namespace VenueSync.Events;

public class VenueData
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
}

public class VenueEnteredData
{
    public required VenueData venue { get; set; }
    public required VenueLocation location { get; set; }
}

public sealed class VenueEntered(): EventWrapper<VenueEnteredData, VenueEntered.Priority>(nameof(VenueEntered))
{
    public enum Priority
    {
        /// <seealso cref="VenueService.OnVenueEntered"/>
        High = 1,
    }
}
