namespace VenueSync.Data.DTO.Locations;

public record SendLocationResponse
{
    public bool success { get; init; } = false;
}
    
public record SendLocationReply
{
    public bool Success { get; init; } = false;
    public bool Graceful { get; init; } = false;
    public string? ErrorMessage { get; set; } = string.Empty;
}