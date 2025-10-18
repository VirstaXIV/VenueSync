using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using VenueSync.Data;
using VenueSync.Events;
using VenueSync.Services.IPC;
using VenueSync.State;
using VenueSync.Ui;

namespace VenueSync.Services;

public class VenueService : IDisposable
{
    private readonly Configuration _configuration;
    private readonly VenueSettings _venueSettings;
    private readonly ActorObjectManager _objects;
    private readonly ITextureProvider _textureProvider;
    private readonly GameStateService _gameStateService;
    private readonly StateService _stateService;
    private readonly SyncFileService _syncFileService;
    private readonly VenueSyncWindowSystem _windowSystem;
    private readonly PenumbraIPC _penumbraIPC;
    private readonly ManipulationDataManager _manipulationDataManager;
    
    private readonly AddTemporaryMod _penumbraAddTemporaryMod;
    private readonly RemoveTemporaryMod _penumbraRemoveTemporaryMod;
    private readonly CreateTemporaryCollection _penumbraCreateNamedTemporaryCollection;
    private readonly AssignTemporaryCollection _penumbraAssignTemporaryCollection;
    private readonly DeleteTemporaryCollection _penumbraRemoveTemporaryCollection;
    private readonly RedrawObject _penumbraRedrawObject;
    
    private Dictionary<string, Guid> _collectionIds = [];
    private bool _hasQueuedReload;
    
    public VenueService(
        IDalamudPluginInterface pluginInterface,
        ActorObjectManager objects,
        ITextureProvider textureProvider,
        Configuration configuration,
        VenueSettings venueSettings,
        GameStateService gameStateService,
        SyncFileService syncFileService,
        StateService stateService,
        VenueSyncWindowSystem windowSystem,
        PenumbraIPC penumbraIPC,
        ManipulationDataManager manipulationDataManager,
        VenueEntered venueEntered,
        VenueUpdated venueUpdated,
        VenueExited venueExited,
        LoggedOut loggedOut,
        ReloadMods reloadMods,
        DisableMods disableMods,
        ServiceDisconnected serviceDisconnected)
    {
        _configuration = configuration;
        _venueSettings = venueSettings;
        _objects = objects;
        _textureProvider = textureProvider;
        _gameStateService = gameStateService;
        _stateService = stateService;
        _syncFileService = syncFileService;
        _windowSystem = windowSystem;
        _penumbraIPC = penumbraIPC;
        _manipulationDataManager = manipulationDataManager;
        
        VenueSync.Log.Debug("Starting Venue Service");
        
        _penumbraAddTemporaryMod = new AddTemporaryMod(pluginInterface);
        _penumbraRemoveTemporaryMod = new RemoveTemporaryMod(pluginInterface);
        _penumbraCreateNamedTemporaryCollection = new CreateTemporaryCollection(pluginInterface);
        _penumbraAssignTemporaryCollection = new AssignTemporaryCollection(pluginInterface);
        _penumbraRemoveTemporaryCollection = new DeleteTemporaryCollection(pluginInterface);
        _penumbraRedrawObject = new RedrawObject(pluginInterface);
        
        venueEntered.Subscribe(OnVenueEntered, VenueEntered.Priority.High);
        venueUpdated.Subscribe(OnVenueUpdated, VenueUpdated.Priority.High);
        venueExited.Subscribe(OnVenueExited, VenueExited.Priority.High);
        loggedOut.Subscribe(DisposeMods, LoggedOut.Priority.High);
        reloadMods.Subscribe(ReloadAllMods, ReloadMods.Priority.High);
        disableMods.Subscribe(DisposeMods, DisableMods.Priority.High);
        serviceDisconnected.Subscribe(OnServiceDisconnected, ServiceDisconnected.Priority.High);
        
        // Monitor download completion to trigger reload
        Task.Run(MonitorDownloadsAsync);
    }

    public void Dispose()
    {
        _stateService.VenueState.logoTexture?.Dispose();
        DisposeMannequins();
    }

    private async Task MonitorDownloadsAsync()
    {
        while (true)
        {
            await Task.Delay(1000);
            
            if (_hasQueuedReload && !_syncFileService.IsDownloading)
            {
                _hasQueuedReload = false;
                VenueSync.Log.Debug("Downloads completed, reloading mods");
                ReloadAllMods();
            }
        }
    }

    private void ReloadAllMods()
    {
        _gameStateService.RunOnFrameworkThread(() =>
        {
            if (_penumbraIPC.IsAvailable && _gameStateService.IsCharacterSet)
            {
                DisposeMannequins();
                HandleMannequins();
            }
        }).ConfigureAwait(false);
    }
    
    private void DisposeMods()
    {
        _gameStateService.RunOnFrameworkThread(() =>
        {
            if (_penumbraIPC.IsAvailable)
            {
                DisposeMannequins();
            }
        }).ConfigureAwait(false);
    }

    private void Redraw()
    {
        var mannequinList = _objects
            .Where(p => p.Value.Objects.Any(a => a.Model))
            .Where(p => p.Key.Type is IdentifierType.Retainer);
        
        foreach (var mannequin in mannequinList)
        {
            if (mannequin.Value.Objects.Count > 0)
            {
                _penumbraRedrawObject.Invoke(mannequin.Value.Objects[0].Index.Index, RedrawType.Redraw);
            }
        }
    }

    private void HandleMannequins()
    {
        var mannequinList = _objects
            .Where(p => p.Value.Objects.Any(a => a.Model))
            .Where(p => p.Key.Type is IdentifierType.Retainer);
        
        foreach (var mannequin in mannequinList)
        {
            VenueSync.Log.Debug($"Found mannequin {mannequin.Key}");
            var loadedMannequin = _stateService.VenueState.location.mannequins
                .FirstOrDefault(m => m.name.Equals(mannequin.Value.Label));

            if (loadedMannequin == null)
            {
                continue;
            }
            
            var mods = _stateService.VenueState.location.mods
                .Where(m => m.mannequin_id.Equals(loadedMannequin.id))
                .ToList();
            
            SetupMod(loadedMannequin, mannequin, mods);
        }

        Redraw();
    }
    
    private void DisposeMannequins()
    {
        foreach (var (uid, _) in _collectionIds)
        {
            RemoveMod(uid);
        }
        
        _collectionIds.Clear();
        Redraw();
    }

    private void LoadMod(string uid, ushort idx, Dictionary<string, string> fileList, string manipulationData)
    {
        try
        {
            var collName = "VenueSync_" + uid;

            if (!_collectionIds.TryGetValue(uid, out var guid))
            {
                VenueSync.Log.Debug($"Creating Temp Collection: {collName}");

                var penEc = _penumbraCreateNamedTemporaryCollection.Invoke(uid, collName, out guid);

                if (penEc != PenumbraApiEc.Success)
                {
                    VenueSync.Log.Error($"Failed to create temporary collection for {collName} with error code {penEc}");
                    return;
                }

                VenueSync.Log.Debug($"Created Temp Collection: {collName}");
            }

            VenueSync.Log.Debug($"Assigning Temp Collection to index {idx}");

            _penumbraAssignTemporaryCollection.Invoke(guid, idx, true);
            _penumbraRemoveTemporaryMod.Invoke("VenueSync", guid, 0);
            _penumbraAddTemporaryMod.Invoke("VenueSync", guid, fileList, manipulationData, 0);

            VenueSync.Log.Debug($"Successfully loaded mod for {collName}");

            _collectionIds[uid] = guid;
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Failed to load mod: {exception.Message}");
        }
    }

    private void SetupMod(MannequinItem mannequin, KeyValuePair<ActorIdentifier, ActorData> mannequinActor, List<MannequinModItem> mods)
    {
        if (_configuration.AutoloadMods ? _venueSettings.InactiveMods.Contains(mannequin.id) : !_venueSettings.ActiveMods.Contains(mannequin.id))
        {
            return;
        }
        
        try
        {
            var idx = mannequinActor.Value.Objects[0].Index.Index;
            var uid = mannequin.id;

            if (!_syncFileService.VerifyModFiles(mods))
            {
                VenueSync.Log.Debug($"Mod files missing for {mannequin.name}, downloading...");
                _syncFileService.DownloadModFiles(mods);
                _hasQueuedReload = true;
                return;
            }

            var manipulationData = _manipulationDataManager.BuildManipulationData(mannequinActor, mods);
            
            VenueSync.Log.Debug($"Setting up mod for {mannequin.name}");
            
            LoadMod(uid, idx, _syncFileService.BuildModFileList(manipulationData.Paths, mods), manipulationData.ManipulationString);
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Failed to setup mod: {exception}");
        }
    }

    private void RemoveMod(string uid)
    {
        if (_collectionIds.TryGetValue(uid, out var guid))
        {
            _penumbraRemoveTemporaryMod.Invoke("VenueSync", guid, 0);
            _penumbraRemoveTemporaryCollection.Invoke(guid);
        }
    }
    
    private async Task LoadLogoTextureAsync(string imagePath)
    {
        try
        {
            _stateService.VenueState.logoTexture?.Dispose();

            await using var fileStream = File.OpenRead(imagePath);
            _stateService.VenueState.logoTexture = await _textureProvider.CreateFromImageAsync(
                fileStream,
                leaveOpen: false,
                cancellationToken: CancellationToken.None
            );
            
            VenueSync.Log.Debug("Logo texture loaded successfully");
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Failed to load logo texture: {ex.Message}");
        }
    }

    private void OnVenueEntered(VenueEnteredData data)
    {
        UpdateVenueState(data.venue, data.location, data.staff, data.tags, data.streams);
        
        _syncFileService.MaybeDownloadFile(
            data.venue.id, 
            data.venue.logo, 
            "png", 
            data.venue.hash, 
            path => _ = Task.Run(async () => await LoadLogoTextureAsync(path))
        );
        
        VenueSync.Messager.NotificationMessage($"Welcome to [{data.venue.name}]", NotificationType.Success);

        if (!_windowSystem.VenueWindowOpened())
        {
            _windowSystem.ToggleVenueWindow();
        }

        ReloadAllMods();
    }

    private void OnVenueUpdated(VenueUpdatedData data)
    {
        UpdateVenueState(data.venue, data.location, data.staff, data.tags, data.streams);
        
        _syncFileService.MaybeDownloadFile(
            data.venue.id, 
            data.venue.logo, 
            "png", 
            data.venue.hash, 
            path => _ = Task.Run(async () => await LoadLogoTextureAsync(path))
        );
        
        ReloadAllMods();
    }

    private void UpdateVenueState(VenueData venue, VenueLocation location, List<VenueStaff> staff, List<string> tags, List<VenueStream> streams)
    {
        _stateService.VenueState.id = venue.id;
        _stateService.VenueState.name = venue.name;
        _stateService.VenueState.location = location;
        _stateService.VenueState.logo = venue.logo;
        _stateService.VenueState.hash = venue.hash;
        _stateService.VenueState.description = venue.description;
        _stateService.VenueState.open_hours = venue.open_hours;
        _stateService.VenueState.discord_invite = venue.discord_invite;
        _stateService.VenueState.carrd_url = venue.carrd_url;
        _stateService.VenueState.staff = staff;
        _stateService.VenueState.tags = tags;
        _stateService.VenueState.streams = streams;
        
        _venueSettings.Load();
    }

    private void OnVenueExited(string id)
    {
        DisposeMods();
        
        if (_windowSystem.VenueWindowOpened())
        {
            _windowSystem.ToggleVenueWindow();
        }
    }

    private void OnServiceDisconnected()
    {
        DisposeMods();
        _stateService.ResetVenueState();
    }
}
