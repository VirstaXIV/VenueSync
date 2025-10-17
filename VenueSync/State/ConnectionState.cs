namespace VenueSync.State;

public record Connection
{
    public bool Connected { get; set; }
    public bool Connecting { get; set; }
    public bool Disconnecting { get; set; }
};
