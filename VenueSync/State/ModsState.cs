using System.Collections.Generic;

namespace VenueSync.State;

public record ModsState
{
    public List<ModItem> modList { get; set; } = [];
    public List<ModPenumbraLink> penumbraLinks { get; set; } = [];
}

public record ModItem
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string version_id { get; set; } = "";
    public bool is_public { get; set; } = false;
    public List<ModVersionItem> versions { get; set; } = [];
}

public record ModVersionItem
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
}

public record ModPenumbraLink
{
    public string mod_id { get; set; } = "";
    public string path { get; set; } = "";
}