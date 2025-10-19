using OtterGui.Classes;
using VenueSync.State;

namespace VenueSync.Events;

public class VenueModData
{
    public required string venue_id { get; set; }
    public required string location_id { get; set; }
    public required MannequinModItem mod { get; set; }
}
public sealed class VenueMod(): EventWrapper<VenueModData, VenueMod.Priority>(nameof(VenueMod))
{
    public enum Priority
    {
        High = 1,
    }
}
