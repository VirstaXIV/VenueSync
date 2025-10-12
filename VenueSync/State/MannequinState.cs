namespace VenueSync.State;

public record Mannequin
{
    public long ffxiv_id { get; set; } = 0;
    public string name { get; set; } = "";
    public string world { get; set; } = "";
    public string data_center { get; set; } = "";
}
