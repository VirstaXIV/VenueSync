using System.Diagnostics.CodeAnalysis;

namespace VenueSync.Data.DTO.Account;

public record XIVAuthRegisterResponse
{
    public string auth_url { get; init; } = "";
    public string poll_url { get; init; } = "";
}

public record XIVAuthPollResponse
{
    public string status { get; init; } = "";
    public string? user_id { get; init; } = "";
    public string? token { get; init; }
}

public record XIVAuthVerification
{
    public bool Success { get; init; } = false;
    public string? ErrorMessage { get; init; } = string.Empty;
    public string? UserID { get; init; } = string.Empty;
    public string? Token { get; init; } = string.Empty;
}
