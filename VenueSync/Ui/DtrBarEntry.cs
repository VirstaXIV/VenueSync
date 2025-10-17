using System;
using System.Threading.Tasks;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using VenueSync.Events;
using VenueSync.Services;

namespace VenueSync.Ui;

public class DtrBarEntry: IDisposable
{
    private readonly IDtrBar _dtrBar;
    private readonly Configuration _configuration;
    private readonly StateService _stateService;
    private readonly SocketService _socketService;
    private readonly VenueSyncWindowSystem _venueSyncWindowSystem;
    
    private readonly UpdateDtrBar _updateDtrBar;
    
    private readonly Lazy<IDtrBarEntry> _entry;
    private string? _text;
    private string? _tooltip;
    
    public DtrBarEntry(IDtrBar dtrBar, Configuration configuration, StateService stateService, VenueSyncWindowSystem venueSyncWindowSystem, SocketService socketService,
        UpdateDtrBar @updateDtrBar)
    {
        _dtrBar = dtrBar;
        _configuration = configuration;
        _stateService = stateService;
        _socketService = socketService;
        _venueSyncWindowSystem = venueSyncWindowSystem;
        
        _updateDtrBar = @updateDtrBar;
        
        _entry = new(CreateEntry);
        
        _updateDtrBar.Subscribe(Update, UpdateDtrBar.Priority.High);
    }
    
    private IDtrBarEntry CreateEntry()
    {
        VenueSync.Log.Debug("Creating VenueSync DtrBar entry");
        var entry = _dtrBar.Get("VenueSync");
        entry.OnClick = OnEntryClick;

        return entry;
    }

    private void OnEntryClick(DtrInteractionEvent interactionEvent)
    {
        if (!_configuration.ServerToken.IsNullOrEmpty())
        {
            // Shift left click for settings menu, and click for venue if inside venue or settings if not
            if (interactionEvent.ClickType.Equals(MouseClickType.Left))
            {
                if (interactionEvent.ModifierKeys.HasFlag(ClickModifierKeys.Shift))
                {
                    _venueSyncWindowSystem.ToggleMainWindow();
                }
                else
                {
                    if (_stateService.VenueState.id != string.Empty)
                    {
                        _venueSyncWindowSystem.ToggleVenueWindow();
                    }
                    else
                    {
                        _venueSyncWindowSystem.ToggleMainWindow();
                    }
                }
                return;
            }

            // On right click, can toggle connecting to service
            if (interactionEvent.ClickType.Equals(MouseClickType.Right))
            {
                if (_stateService.Connection is { Connecting: false, Disconnecting: false })
                {
                    _ = _stateService.Connection.Connected ? 
                            Task.Run(async () => await _socketService.DisconnectAsync(true).ConfigureAwait(false)) : 
                            Task.Run(async () => await _socketService.ConnectAsync().ConfigureAwait(false));
                }
            }
        }
    }

    private void UpdateEntry()
    {
        string text;
        string tooltip;

        if (_stateService.Connection.Connected)
        {
            if (_stateService.VenueState.id != string.Empty)
            {
                var venue = _stateService.VenueState;
                text = $"\uE048 {venue.name}";
                tooltip = $"VenueSync: Connected{Environment.NewLine}{venue.name}: {venue.location.world} - {venue.location.district} - Ward {venue.location.ward}, Plot {venue.location.plot}";
            }
            else
            {
                text = $"\uE048 Not in Venue";
                tooltip = "VenueSync: Connected";
            }
        }
        else
        {
            text = $"\uE048 \uE04C";
            tooltip = "VenueSync: Not Connected";
        }
        
        var entry = _entry.Value;
        if (!entry.Shown)
        {
            entry.Shown = true;
        }

        bool needsUpdate =
            !string.Equals(text, _text, StringComparison.Ordinal) ||
            !string.Equals(tooltip, _tooltip, StringComparison.Ordinal);

        if (needsUpdate)
        {
            var builder = new SeStringBuilder();
            builder.AddText(text);
            entry.Text = builder.Build();
            entry.Tooltip = tooltip;
            _text = text;
            _tooltip = tooltip;
        }
    }
    
    private void HideEntry()
    {
        if (_entry.IsValueCreated && _entry.Value.Shown)
        {
            _entry.Value.Shown = false;
        }

    }

    private void Update()
    {
        if (_configuration.EnableDtrBar && !_configuration.ServerToken.IsNullOrEmpty())
        {
            UpdateEntry();
        }
        else
        {
            HideEntry();
        }
    }

    public void Dispose()
    {
        if (_entry.IsValueCreated)
        {
            _entry.Value.Remove();
        }
    }
}
