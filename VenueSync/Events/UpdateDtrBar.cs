using OtterGui.Classes;

namespace VenueSync.Events;

public sealed class UpdateDtrBar(): EventWrapper<UpdateDtrBar.Priority>(nameof(UpdateDtrBar))
{
    public enum Priority
    {
        None = 0,
        High = 1,
    }
}