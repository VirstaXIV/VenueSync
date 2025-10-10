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

            _services.GetService<VenueSyncWindowSystem>();
            _services.GetService<CommandService>();
            _services.GetService<TerritoryWatcher>();
            _services.GetService<PlayerWatcher>();
            _services.GetService<VenueService>();
            _services.GetService<SocketService>();
            Log.Information($"VenueSync v{Version} loaded successfully.");
        }
        catch (Exception)
        {
            Log.Fatal($"VenueSync v{Version} failed to load.");
            Dispose();
            throw;
        }
    }
    
    public void Dispose()
    {
        _services?.Dispose();
    }
}
