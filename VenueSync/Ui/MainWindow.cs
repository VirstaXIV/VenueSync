using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Services;
using OtterGui.Text;
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

    private readonly FileDialogManager _fileDialogManager;
    private readonly Configuration _configuration;
    private readonly SocketService _socketService;
    private readonly StateService _stateService;
    private readonly VenueWindow _venueWindow;
    private readonly TabSelected _event;
    private readonly MainWindowPosition _position;
    private ITab[] _tabs;

    private readonly SettingsTab _settings;
    private readonly VenuesTab _venues;
    private readonly HousesTab _houses;
    private readonly CharactersTab _characters;

    private TabType _selectTab;

    public MainWindow(
        IDalamudPluginInterface pluginInterface, FileDialogManager fileDialogManager, Configuration configuration, StateService stateService,
        SettingsTab settings, VenueWindow venueWindow, VenuesTab venueTab, CharactersTab charactersTab, HousesTab housesTab,
        TabSelected @event, MainWindowPosition position, SocketService socketService) : base("VenueSyncMainWindow")
    {
        pluginInterface.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(600, 400),
        };
        _settings = settings;
        _venues = venueTab;
        _houses = housesTab;
        _characters = charactersTab;
        _fileDialogManager = fileDialogManager;
        _configuration = configuration;
        _socketService = socketService;
        _stateService = stateService;
        _venueWindow = venueWindow;
        _event = @event;
        _position = position;
        _tabs = [
            _settings
        ];

        _selectTab = _configuration.Ephemeral.SelectedTab;
        _event.Subscribe(OnTabSelected, TabSelected.Priority.MainWindow);
        IsOpen = _configuration.OpenWindowAtStart;
    }

    public void OpenSettings()
    {
        IsOpen = true;
        _selectTab = TabType.Settings;
    }

    public override void PreDraw()
    {
        _position.IsOpen = IsOpen;
        WindowName = $"VenueSync v{VenueSync.Version}###VenueSyncMainWindow";
    }

    public void Dispose()
    {
        _event.Unsubscribe(OnTabSelected);
        _fileDialogManager.Reset();
    }

    public override void Draw()
    {
        _position.Size = ImGui.GetWindowSize();
        _position.Position = ImGui.GetWindowPos();

        if (!_socketService.Connected)
        {
            _tabs = [
                _settings
            ];
        }
        else
        {
            _tabs = [
                _characters,
                _houses,
                _venues,
                _settings
            ];
        }

        if (_stateService.VenueState.id != string.Empty)
        {
            if (ImUtf8.Button($"Toggle {_stateService.VenueState.name} Window"))
            {
                _venueWindow.Toggle();
            }
        }

        if (TabBar.Draw("##tabs", ImGuiTabBarFlags.None, ToLabel(_selectTab), out var currentTab, () => { }, _tabs))
            _selectTab = TabType.None;
        var tab = FromLabel(currentTab);

        if (tab != _configuration.Ephemeral.SelectedTab)
        {
            _configuration.Ephemeral.SelectedTab = FromLabel(currentTab);
            _configuration.Ephemeral.Save();
        }
    }

    private ReadOnlySpan<byte> ToLabel(TabType type)
        => type switch {
            TabType.Settings => _settings.Label,
            TabType.Characters => _characters.Label,
            TabType.Houses => _houses.Label,
            TabType.Venues => _venues.Label,
            _ => ReadOnlySpan<byte>.Empty,
        };

    private TabType FromLabel(ReadOnlySpan<byte> label)
    {
        // @formatter:off
        if (label == _settings.Label)   return TabType.Settings;
        if (label == _characters.Label)   return TabType.Characters;
        if (label == _houses.Label)   return TabType.Houses;
        if (label == _venues.Label)   return TabType.Venues;
        // @formatter:on
        return TabType.None;
    }

    private void OnTabSelected(TabType type)
    {
        _selectTab = type;
        IsOpen = true;
    }
}
