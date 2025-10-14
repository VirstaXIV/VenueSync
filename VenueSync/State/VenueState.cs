using System.Collections.Generic;
using Dalamud.Interface.Textures.TextureWraps;

namespace VenueSync.State;

public record VenueData
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string logo { get; set; } = "";
    public string hash { get; set; } = "";
}

public record MannequinItem
{
    public string id { get; set; } = "";
    public string name { get; init; } = "";
}

public record MannequinModFileItem
{
    public string id { get; init; } = "";
    public string path { get; set; } = "";
    public string file { get; init; } = "";
    public string hash { get; init; } = "";
    public string extension { get; init; } = "";
}

public record MannequinModItem
{
    public string id { get; init; } = "";
    public string name { get; set; } = "";
    public string version { get; init; } = "";
    public string file { get; init; } = "";
    public string hash { get; init; } = "";
    public string mannequin_id { get; init; } = "";
    public string extension { get; init; } = "";
    public List<MannequinModFileItem> files { get; init; } = [];
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
    public List<MannequinModItem> mods { get; init; } = [];
}

public record VenueState
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string logo { get; set; } = "";
    public string hash { get; set; } = "";
    public IDalamudTextureWrap? logoTexture;
    public required VenueLocation location { get; set; }
}
