namespace VenueSync.Data.DTO.Locations;

public record VerifyLocationResponse
{
    public bool success { get; init; } = false;
    public string message { get; init; } = "";
}
    
public record VerifyLocationReply
{
    public bool Success { get; init; } = false;
    public bool Graceful { get; init; } = false;
    public string? ErrorMessage { get; set; } = string.Empty;
}