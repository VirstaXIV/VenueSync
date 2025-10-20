using System.Collections.Generic;

namespace VenueSync.State;

public record UserHouseMannequinItem
{
    public string id { get; set; } = "";
    public string name { get; init; } = "";
}

public record UserVenueStreamItem
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string type { get; set; } = "";
}

public record UserVenueScheduleItem
{
    public string id { get; set; } = "";
    public int day { get; set; } = 0;
    public string start_time { get; set; } = "";
    public string end_time { get; set; } = "";
    public string timezone { get; set; } = "";
}

public record UserVenueStaffItem
{
    public string id { get; set; } = "";
    public string lodestone_id { get; set; } = "";
    public string name { get; set; } = "";
    public string position { get; set; } = "";
    public bool granted { get; set; } = false;
}

public record UserVenueLocationItem
{
    public string id { get; set; } = "";
    public string house_id { get; set; } = "";
    public string name { get; set; } = "";
    public List<MannequinModItem> mods { get; set; } = [];
}

public record UserVenueItem
{
    public string id { get; set; } = "";
    public string name { get; init; } = "";
    public string discord_invite { get; init; } = "";
    public string carrd_url { get; init; } = "";
    public string description { get; init; } = "";
    public string logo { get; init; } = "";
    public string hash { get; init; } = "";
    public List<string> tags { get; set; } = new();
    public List<UserVenueStaffItem> staff { get; set; } = [];
    public List<UserVenueStreamItem> streams { get; set; } = [];
    public List<UserVenueLocationItem> locations { get; set; } = [];
    public List<UserVenueScheduleItem> schedules { get; set; } = [];
}

public record UserHouseItem
{
    public string id { get; init; } = "";
    public string type { get; init; } = "";
    public string owner { get; init; } = "";
    public string name { get; init; } = "";
    public string district { get; init; } = "";
    public int ward { get; init; } = 0;
    public int plot { get; init; } = 0;
    public string size { get; init; } = "";
    public string world { get; init; } = "";
    public string data_center { get; init; } = "";
    public bool verified { get; init; } = false;
    public bool perms { get; init; } = false;
    public List<UserHouseMannequinItem> mannequins { get; set; } = [];
}

public record UserCharacterItem
{
    public string id { get; set; } = "";
    public string name { get; init; } = "";
    public int main { get; init; } = 0;
    public string lodestone_id { get; init; } = "";
    public string world { get; init; } = "";
    public string data_center { get; init; } = "";
}

public record UserState
{
    public required List<UserVenueItem> venues { get; set; }
    public required List<UserHouseItem> houses { get; set; }
    public required List<UserCharacterItem> characters { get; set; }
}
