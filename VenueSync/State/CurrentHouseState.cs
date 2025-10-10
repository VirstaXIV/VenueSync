namespace VenueSync.State;

public record House
{
    public long HouseId { get; set; } = 0;
    public int Plot { get; set; } = 0;
    public int Ward { get; set; } = 0;
    public int Room { get; set; } = 0;
    public string District { get; set; } = "";
    public uint? WorldId { get; set; } = 0;
    public string Type { get; set; } = "";
    public string WorldName { get; set; } = "";
    public string DataCenter { get; set; } = "";
};
