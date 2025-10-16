using OtterGui.Classes;

namespace VenueSync.Events;

public class DiceRollData
{
    public string name { get; set; } = string.Empty;
    public ushort homeWorldId { get; set; }
    public int roll { get; set; }
    public int outOf { get; set; }
}
public sealed class DiceRoll(): EventWrapper<DiceRollData, DiceRoll.Priority>(nameof(DiceRoll))
{
    public enum Priority
    {
        None = 0,
        High = 1,
    }
}