using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.DragDrop;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Classes;
using OtterGui.Services;
using OtterGui.Log;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Interop;
using VenueSync.Events;
using VenueSync.Services.IPC;
using VenueSync.Ui;
using VenueSync.Ui.Tabs.CharactersTab;
using VenueSync.Ui.Tabs.HousesTab;
using VenueSync.Ui.Tabs.SettingsTab;
using VenueSync.Ui.Tabs.VenuesTab;

namespace VenueSync.Services;

public static class ServiceProvider
{
    public static ServiceManager CreateProvider(IDalamudPluginInterface pluginInterface, Logger log, VenueSync venueSync)
    {
        EventWrapperBase.ChangeLogger(log);

        var services = new ServiceManager(log)
                       .AddExistingService(log)
                       .AddDalamudServices(pluginInterface)
                       .AddMeta()
                       .AddEvents()
                       .AddInterop()
                       .AddApi()
                       .AddUi()
                       .AddIPC()
                       .AddExistingService(venueSync);

        services.CreateProvider();
        return services;
    }

    private static ServiceManager AddDalamudServices(this ServiceManager services, IDalamudPluginInterface pluginInterface)
        => services.AddExistingService(pluginInterface)
                   .AddExistingService(pluginInterface.UiBuilder)
                   .AddSingleton<FileDialogManager>()
                   .AddDalamudService<ICommandManager>(pluginInterface)
                   .AddDalamudService<IDataManager>(pluginInterface)
                   .AddDalamudService<IClientState>(pluginInterface)
                   .AddDalamudService<ICondition>(pluginInterface)
                   .AddDalamudService<IGameGui>(pluginInterface)
                   .AddDalamudService<IChatGui>(pluginInterface)
                   .AddDalamudService<IFramework>(pluginInterface)
                   .AddDalamudService<ITargetManager>(pluginInterface)
                   .AddDalamudService<IObjectTable>(pluginInterface)
                   .AddDalamudService<IKeyState>(pluginInterface)
                   .AddDalamudService<IDragDropManager>(pluginInterface)
                   .AddDalamudService<ITextureProvider>(pluginInterface)
                   .AddDalamudService<IPluginLog>(pluginInterface)
                   .AddDalamudService<IGameInteropProvider>(pluginInterface)
                   .AddDalamudService<INotificationManager>(pluginInterface)
                   .AddDalamudService<IContextMenu>(pluginInterface)
                   .AddDalamudService<ISeStringEvaluator>(pluginInterface);

    private static ServiceManager AddMeta(this ServiceManager services)
        => services.AddSingleton<MessageService>()
                   .AddSingleton<FilenameManager>()
                   .AddSingleton<FrameworkManager>()
                   .AddSingleton<SaveService>()
                   .AddSingleton<Configuration>()
                   .AddSingleton<EphemeralConfig>()
                   .AddSingleton<StateService>()
                   .AddSingleton<FileService>()
                   .AddSingleton<PluginWatcherService>()
                   .AddSingleton<CommandService>();
    
    private static ServiceManager AddIPC(this ServiceManager services)
        => services.AddSingleton<PenumbraIPC>()
                   .AddSingleton<IPCManager>();

    private static ServiceManager AddEvents(this ServiceManager services)
        => services.AddSingleton<TabSelected>()
                   .AddSingleton<ServiceConnected>()
                   .AddSingleton<VenueEntered>();
    
    private static ServiceManager AddInterop(this ServiceManager services)
        => services.AddSingleton<NameDicts>()
                   .AddSingleton<DictWorld>()
                   .AddSingleton<DictMount>()
                   .AddSingleton<DictCompanion>()
                   .AddSingleton<DictOrnament>()
                   .AddSingleton<DictBNpc>()
                   .AddSingleton<DictENpc>()
                   .AddSingleton(p => new CutsceneResolver(p.GetRequiredService<PenumbraIPC>().CutsceneParent))
                   .AddSingleton<ActorManager>()
                   .AddSingleton<ObjectManager>()
                   .AddSingleton<ActorObjectManager>();


    private static ServiceManager AddApi(this ServiceManager services)
        => services.AddSingleton<AccountService>()
                   .AddSingleton<SocketService>()
                   .AddSingleton<LocationService>()
                   .AddSingleton<MannequinService>()
                   .AddSingleton<TerritoryWatcher>()
                   .AddSingleton<PlayerWatcher>()
                   .AddSingleton<VenueService>();

    private static ServiceManager AddUi(this ServiceManager services)
        => services.AddSingleton<SettingsTab>()
                   .AddSingleton<VenuesTab>()
                   .AddSingleton<HousesTab>()
                   .AddSingleton<CharactersTab>()
                   .AddSingleton<MainWindowPosition>()
                   .AddSingleton<MainWindow>()
                   .AddSingleton<VenueWindowPosition>()
                   .AddSingleton<VenueWindow>()
                   .AddSingleton<HouseVerifyWindowPosition>()
                   .AddSingleton<HouseVerifyWindow>()
                   .AddSingleton<ManageMannequinsWindowPosition>()
                   .AddSingleton<ManageMannequinsWindow>()
                   .AddSingleton<VenueSyncWindowSystem>();
}