using System;
using Dalamud.Plugin;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;

namespace VenueSync.Services.IPC;

public class PenumbraIPC: IDisposable
{
    private string? _penumbraModDirectory;

    public bool IsAvailable { get; private set; } = false;
    
    private readonly EventSubscriber _penumbraDispose;
    private readonly EventSubscriber _penumbraInit;
    private readonly GetModDirectory _penumbraResolveModDir;
    private readonly GetEnabledState _penumbraEnabled;
    public string? ModDirectory
    {
        get => _penumbraModDirectory;
        private set
        {
            if (!string.Equals(_penumbraModDirectory, value, StringComparison.Ordinal))
            {
                _penumbraModDirectory = value;
            }
        }
    }
    
    private bool _pluginLoaded;
    private Version _pluginVersion;
    
    public PenumbraIPC(IDalamudPluginInterface pluginInterface)
    {
        _penumbraInit = Initialized.Subscriber(pluginInterface, PenumbraInit);
        _penumbraDispose = Disposed.Subscriber(pluginInterface, PenumbraDispose);
        _penumbraResolveModDir = new GetModDirectory(pluginInterface);
        _penumbraEnabled = new GetEnabledState(pluginInterface);
        
        var plugin = PluginWatcherService.GetInitialPluginState(pluginInterface, "Penumbra");
        
        _pluginLoaded = plugin?.IsLoaded ?? false;
        _pluginVersion = plugin?.Version ?? new(0, 0, 0, 0);

        CheckModDirectory();
        CheckAPI();
    }

    public void CheckAPI()
    {
        bool penumbraAvailable = false;
        try
        {
            penumbraAvailable = _pluginLoaded && _pluginVersion >= new Version(1, 5, 1, 0);
            try
            {
                penumbraAvailable &= _penumbraEnabled.Invoke();
            }
            catch
            {
                penumbraAvailable = false;
            }
            IsAvailable = penumbraAvailable;
        }
        catch
        {
            IsAvailable = penumbraAvailable;
        }
    }
    
    public void CheckModDirectory()
    {
        if (!IsAvailable)
        {
            ModDirectory = string.Empty;
        }
        else
        {
            ModDirectory = _penumbraResolveModDir!.Invoke().ToLowerInvariant();
        }
    }

    public void Dispose()
    {
        
    }
    
    private void PenumbraDispose()
    {
        
    }

    private void PenumbraInit()
    {
        IsAvailable = true;
        ModDirectory = _penumbraResolveModDir.Invoke();
    }
}
