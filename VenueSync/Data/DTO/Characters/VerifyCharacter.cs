namespace VenueSync.Data.DTO.Characters;

public record VerifyCharacterResponse
{
    public bool success { get; init; } = false;
    public string? message { get; init; } = string.Empty;
}

public record VerifyCharacterReply
{
    public bool Success { get; init; } = false;
    public bool Graceful { get; init; } = false;
    public string? ErrorMessage { get; set; } = string.Empty;
}
