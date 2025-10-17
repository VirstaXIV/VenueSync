using System;
using Dalamud.Plugin;
using OtterGui.Classes;
using OtterGui.Log;
using OtterGui.Services;
using System.Reflection;
using Dalamud.Plugin.Services;
using VenueSync.Services;
using VenueSync.Ui;

namespace VenueSync;

public sealed class VenueSync : IDalamudPlugin
{
    public string Name => "VenueSync";
    
    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    public static readonly string CommitHash =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
    
    public static readonly Logger         Log = new();
    public static          MessageService Messager { get; private set; } = null!;
    public static          IDataManager DataManager { get; private set; } = null!;

    private readonly ServiceManager _services;
    
    public VenueSync(IDalamudPluginInterface pluginInterface)
    {
        try
        {
            _services = ServiceProvider.CreateProvider(pluginInterface, Log, this);
            Messager = _services.GetService<MessageService>();
            DataManager = _services.GetService<IDataManager>();
            
            _services.EnsureRequiredServices();

            Log.Debug("VenueSync Initialized");
            _services.GetService<GameStateService>();
            Log.Debug("Loading Windows");
            _services.GetService<VenueSyncWindowSystem>();
            _services.GetService<DtrBarEntry>();
            Log.Debug("Loading Commands");
            _services.GetService<CommandService>();
            Log.Debug("Loading Territory Watcher");
            _services.GetService<TerritoryWatcher>();
            Log.Debug("Loading Sockets");
            _services.GetService<SocketService>();
            Log.Information($"VenueSync v{Version} loaded successfully.");

            try
            {
                Log.Debug("Loading Venue Service");
                _services.GetService<VenueService>();
            }
            catch (Exception exception)
            {
                Log.Error($"Failed to load Venue Sync: {exception.Message}");
            }
        }
        catch (Exception exception)
        {
            Log.Fatal($"VenueSync v{Version} failed to load: {exception.Message}");
            Dispose();
            throw;
        }
    }
    
    public void Dispose()
    {
        _services?.Dispose();
    }
}
