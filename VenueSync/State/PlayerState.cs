namespace VenueSync.State;

public record PlayerState
{
    public string name { get; set; } = "";
    public string world { get; set; } = "";
    public string data_center { get; set; } = "";
}
