using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Services;
using OtterGui.Widgets;
using VenueSync.Events;
using VenueSync.Services;
using VenueSync.Ui.Tabs;

namespace VenueSync.Ui;

public class MainWindowPosition : IService
{
    public bool IsOpen { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
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
    private readonly StateService _stateService;
    private readonly VenueWindow _venueWindow;
    private readonly TabSelected _event;
    private readonly LocationChanged _locationChanged;
    private readonly MainWindowPosition _position;

    private readonly SettingsTab _settings;
    private readonly VenuesTab _venues;
    private readonly HousesTab _houses;
    private readonly CharactersTab _characters;

    private ITab[] _tabs;
    private TabType _selectTab;

    public MainWindow(
        IDalamudPluginInterface pluginInterface,
        FileDialogManager fileDialogManager,
        Configuration configuration,
        StateService stateService,
        SettingsTab settings,
        VenueWindow venueWindow,
        VenuesTab venueTab,
        CharactersTab charactersTab,
        HousesTab housesTab,
        TabSelected @event,
        LocationChanged locationChanged,
        MainWindowPosition position) : base("VenueSyncMainWindow")
    {
        pluginInterface.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(600, 400)
        };

        _fileDialogManager = fileDialogManager;
        _configuration = configuration;
        _stateService = stateService;
        _venueWindow = venueWindow;
        _event = @event;
        _locationChanged = locationChanged;
        _position = position;

        _settings = settings;
        _venues = venueTab;
        _houses = housesTab;
        _characters = charactersTab;

        _tabs = [_settings];
        _selectTab = TabType.Settings;

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

        DrawVenueButtons();

        if (IsClientMode())
        {
            DrawClientModeContent();
        }
        else
        {
            DrawTabs();
        }
    }

    private bool IsClientMode()
    {
        return !_stateService.Connection.Connected || _configuration.ClientMode;
    }

    private void DrawClientModeContent()
    {
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), "Settings");
        ImGui.Separator();
        ImGui.Spacing();
        
        _settings.DrawContent();
    }

    private void DrawVenueButtons()
    {
        if (_stateService.VenueState.id == string.Empty)
            return;

        if (ImGui.Button("Toggle Venue Window"))
        {
            _venueWindow.Toggle();
        }

        ImGui.SameLine();

        if (ImGui.Button("Check Venue"))
        {
            _locationChanged.Invoke();
        }
    }

    private void DrawTabs()
    {
        _tabs = [_characters, _houses, _venues, _settings];

        if (TabBar.Draw("##tabs", ImGuiTabBarFlags.None, ToLabel(_selectTab), out var currentTab, () => { }, _tabs))
        {
            _selectTab = TabType.None;
        }
    }

    private ReadOnlySpan<byte> ToLabel(TabType type)
        => type switch
        {
            TabType.Settings => _settings.Label,
            TabType.Characters => _characters.Label,
            TabType.Houses => _houses.Label,
            TabType.Venues => _venues.Label,
            _ => ReadOnlySpan<byte>.Empty
        };

    private void OnTabSelected(TabType type)
    {
        _selectTab = type;
        IsOpen = true;
    }
}
