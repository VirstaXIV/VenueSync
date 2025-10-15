using System.Collections.Generic;
using OtterGui.Classes;
using VenueSync.State;

namespace VenueSync.Events;

public class VenueUpdatedData
{
    public required VenueData venue { get; set; }
    public required VenueLocation location { get; set; }
    public List<VenueStaff> staff { get; set; } = [];
    public List<VenueStream> streams { get; set; } = [];
    public List<string> tags { get; set; } = [];
}
public sealed class VenueUpdated(): EventWrapper<VenueUpdatedData, VenueUpdated.Priority>(nameof(VenueUpdated))
{
    public enum Priority
    {
        High = 1,
    }
}
