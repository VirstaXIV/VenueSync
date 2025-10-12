using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Services;
using OtterGui.Widgets;
using VenueSync.Events;
using VenueSync.Services;
using VenueSync.Ui.Tabs.CharactersTab;
using VenueSync.Ui.Tabs.HousesTab;
using VenueSync.Ui.Tabs.SettingsTab;
using VenueSync.Ui.Tabs.VenuesTab;

namespace VenueSync.Ui;

public class MainWindowPosition : IService
{
    public bool    IsOpen   { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size     { get; set; }
}

public class MainWindow : Window, IDisposable
{
    public enum TabType
    {
        None = -1,
        Settings = 0,
        Venues = 1,
        Houses = 2,
        Characters = 3
    }

    private readonly Configuration _configuration;
    private readonly SocketService _socketService;
    private readonly TabSelected _event;
    private readonly MainWindowPosition _position;
    private ITab[] _tabs;
    
    public readonly SettingsTab Settings;
    public readonly VenuesTab Venues;
    public readonly HousesTab Houses;
    public readonly CharactersTab Characters;

    public TabType SelectTab;

    public MainWindow(IDalamudPluginInterface pluginInterface, Configuration configuration, 
        SettingsTab settings, VenuesTab venueTab, CharactersTab charactersTab, HousesTab housesTab,
        TabSelected @event, MainWindowPosition position, SocketService socketService) : base("VenueSyncMainWindow")
    {
        pluginInterface.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(600, 400),
        };
        Settings   = settings;
        Venues = venueTab;
        Houses = housesTab;
        Characters = charactersTab;
        _configuration = configuration;
        _socketService = socketService;
        _event = @event;
        _position = position;
        _tabs =
        [
            settings
        ];

        SelectTab = _configuration.Ephemeral.SelectedTab;
        _event.Subscribe(OnTabSelected, TabSelected.Priority.MainWindow);
        IsOpen = _configuration.OpenWindowAtStart;
    }

    public void OpenSettings()
    {
        IsOpen = true;
        SelectTab = TabType.Settings;
    }

    public override void PreDraw()
    {
        _position.IsOpen = IsOpen;
        WindowName = $"VenueSync v{VenueSync.Version}###VenueSyncMainWindow";
    }

    public void Dispose() => _event.Unsubscribe(OnTabSelected);

    public override void Draw()
    {
        _position.Size = ImGui.GetWindowSize();
        _position.Position = ImGui.GetWindowPos();

        if (!_socketService.Connected)
        {
            _tabs = [
                Settings
            ];
        }
        else
        {
            _tabs = [
                Characters,
                Houses,
                Venues,
                Settings
            ];
        }

        if (TabBar.Draw("##tabs", ImGuiTabBarFlags.None, ToLabel(SelectTab), out var currentTab, () => { }, _tabs))
            SelectTab = TabType.None;
        var tab = FromLabel(currentTab);

        if (tab != _configuration.Ephemeral.SelectedTab)
        {
            _configuration.Ephemeral.SelectedTab = FromLabel(currentTab);
            _configuration.Ephemeral.Save();
        }
    }

    private ReadOnlySpan<byte> ToLabel(TabType type)
        => type switch {
            TabType.Settings => Settings.Label,
            TabType.Characters => Characters.Label,
            TabType.Houses => Houses.Label,
            TabType.Venues => Venues.Label,
            _ => ReadOnlySpan<byte>.Empty,
        };

    private TabType FromLabel(ReadOnlySpan<byte> label)
    {
        // @formatter:off
        if (label == Settings.Label)   return TabType.Settings;
        if (label == Characters.Label)   return TabType.Characters;
        if (label == Houses.Label)   return TabType.Houses;
        if (label == Venues.Label)   return TabType.Venues;
        // @formatter:on
        return TabType.None;
    }

    private void OnTabSelected(TabType type)
    {
        SelectTab = type;
        IsOpen = true;
    }
}
