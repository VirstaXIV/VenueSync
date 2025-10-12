namespace VenueSync.Data.DTO.Mannequins;

public record UpdateMannequinResponse
{
    public bool success { get; init; } = false;
    public string message { get; init; } = "";
}
    
public record UpdateMannequinReply
{
    public bool Success { get; init; } = false;
    public bool Graceful { get; init; } = false;
    public string? ErrorMessage { get; set; } = string.Empty;
}
