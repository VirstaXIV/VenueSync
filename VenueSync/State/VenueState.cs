using System.Collections.Generic;

namespace VenueSync.State;

public record VenueData
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
}

public record MannequinItem
{
    public string id { get; set; } = "";
    public string name { get; init; } = "";
}

public record VenueLocation
{
    public string name { get; set; } = "";
    public string type { get; set; } = "";
    public string house_name { get; set; } = "";
    public string district { get; set; } = "";
    public int ward { get; set; } = 0;
    public int plot { get; set; } = 0;
    public string size { get; set; } = "";
    public string world { get; set; } = "";
    public string data_center { get; set; } = "";
    public List<MannequinItem> mannequins { get; init; } = [];
}

public record VenueState
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public required VenueLocation location { get; set; }
}
