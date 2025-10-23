using System.Collections.Generic;
using VenueSync.State;

namespace VenueSync.Data.DTO.Account;
public record UserResponse
{
    public required List<UserVenueItem> venues { get; set; }
    public required List<UserHouseItem> houses { get; set; }
    public required List<UserCharacterItem> characters { get; set; }
    public required List<ModItem> mods { get; set; }
}

public record UserReply
{
    public bool Success { get; init; } = false;
    public bool Graceful { get; init; } = false;
    public string? ErrorMessage { get; init; } = string.Empty;
}