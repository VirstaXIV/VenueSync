using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Services;
using OtterGui.Widgets;
using VenueSync.Events;
using VenueSync.Ui.Tabs.SettingsTab;

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
        Settings = 0
    }

    private readonly Configuration _configuration;
    private readonly TabSelected _event;
    private readonly MainWindowPosition _position;
    private readonly ITab[] _tabs;
    
    public readonly SettingsTab Settings;

    public TabType SelectTab;

    public MainWindow(IDalamudPluginInterface pluginInterface, Configuration configuration, SettingsTab settings, TabSelected @event, MainWindowPosition position) : base("VenueSyncMainWindow")
    {
        pluginInterface.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(700, 675),
            MaximumSize = ImGui.GetIO().DisplaySize,
        };
        Settings   = settings;
        _configuration = configuration;
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
            _ => ReadOnlySpan<byte>.Empty,
        };

    private TabType FromLabel(ReadOnlySpan<byte> label)
    {
        // @formatter:off
        if (label == Settings.Label)   return TabType.Settings;
        // @formatter:on
        return TabType.None;
    }

    private void OnTabSelected(TabType type)
    {
        SelectTab = type;
        IsOpen = true;
    }
}
