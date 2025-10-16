using System.Collections.Generic;
using VenueSync.Data;

namespace VenueSync.State;

public record VisitorsState
{
    public int count { get; set; } = 0;
    public bool SortCurrentVisitorsTop { get; set; } = false;
    public Dictionary<string, Player> players { get; set; } = [];
};
