using System;
using Dalamud.Plugin;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;

namespace VenueSync.Services.IPC;

public class PenumbraIPC: IDisposable
{
    private string? _penumbraModDirectory;
    private Func<int, int>? _checkCutsceneParent;

    public bool IsAvailable { get; private set; } = false;
    public short CutsceneParent(ushort idx)
        => (short)(_checkCutsceneParent?.Invoke(idx) ?? -1);
    
    private readonly IDalamudPluginInterface _pluginInterface;
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
    
    private readonly bool _pluginLoaded;
    private readonly Version _pluginVersion;
    
    public PenumbraIPC(IDalamudPluginInterface pluginInterface)
    {
        _penumbraInit = Initialized.Subscriber(pluginInterface, PenumbraInit);
        _penumbraDispose = Disposed.Subscriber(pluginInterface, PenumbraDispose);
        _penumbraResolveModDir = new GetModDirectory(pluginInterface);
        _penumbraEnabled = new GetEnabledState(pluginInterface);
        _pluginInterface = pluginInterface;
        
        var plugin = PluginWatcherService.GetInitialPluginState(pluginInterface, "Penumbra");
        
        _pluginLoaded = plugin?.IsLoaded ?? false;
        _pluginVersion = plugin?.Version ?? new(0, 0, 0, 0);

        PenumbraInit();
    }
    
    public void CheckModDirectory()
    {
        ModDirectory = !IsAvailable ? string.Empty : _penumbraResolveModDir!.Invoke().ToLowerInvariant();
    }

    public void PenumbraInit()
    {
        try
        {
            PenumbraDispose();
            
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

            if (!IsAvailable)
            {
                return;
            }

            CheckModDirectory();
            _checkCutsceneParent = new GetCutsceneParentIndexFunc(_pluginInterface).Invoke();

        }
        catch
        {
            IsAvailable = false;
        }
    }

    public void Dispose()
    {
        PenumbraDispose();
    }
    
    private void PenumbraDispose()
    {
        if (!IsAvailable)
        {
            return;
        }
        _checkCutsceneParent = null;
        IsAvailable = false;
    }
}
