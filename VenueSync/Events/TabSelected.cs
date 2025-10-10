using OtterGui.Classes;
using VenueSync.Ui;

namespace VenueSync.Events;

public sealed class TabSelected(): EventWrapper<MainWindow.TabType, TabSelected.Priority>(nameof(TabSelected))
{
    public enum Priority
    {
        /// <seealso cref="Ui.MainWindow.OnTabSelected"/>
        MainWindow = 1,
    }
}
