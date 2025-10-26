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
    private HashSet<string> _failedMods = [];
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
        VenueMod venueMod,
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
        venueMod.Subscribe(OnVenueMod, VenueMod.Priority.High);
        
        Task.Run(MonitorDownloadsAsync);
    }

    public void Dispose()
    {
        _stateService.VenueState.logoTexture?.Dispose();
        DisposeStreamTextures();
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
                // If local preview is active, skip re-applying venue mods.
                if (_stateService.VenueState.mods_preview_active)
                {
                    VenueSync.Log.Debug("Skipping venue mod reload due to active local preview.");
                    return;
                }

                _failedMods.Clear();
                _stateService.VenueState.failed_mods.Clear();
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
                _penumbraRedrawObject.Invoke(mannequin.Value.Objects[0].Index.Index);
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
            
            SetupMods(loadedMannequin, mannequin, mods);
        }

        Redraw();
    }
    
    private void DisposeMannequins()
    {
        foreach (var (uid, _) in _collectionIds)
        {
            RemoveCollection(uid);
        }
        
        _collectionIds.Clear();
        Redraw();
    }

    private void LoadMod(string mannequinId, string modId, ushort idx, Dictionary<string, string> fileList, string manipulationData, string modLogName)
    {
        try
        {
            var collName = "VenueSync_" + mannequinId;

            if (!_collectionIds.TryGetValue(mannequinId, out var guid))
            {
                VenueSync.Log.Debug($"Creating Temp Collection: {collName}");

                var penEc = _penumbraCreateNamedTemporaryCollection.Invoke(mannequinId, collName, out guid);

                if (penEc != PenumbraApiEc.Success)
                {
                    VenueSync.Log.Error($"Failed to create temporary collection for {collName} with error code {penEc}");
                    return;
                }

                VenueSync.Log.Debug($"Created Temp Collection: {collName}");

                VenueSync.Log.Debug($"Assigning Temp Collection to index {idx}");
                _penumbraAssignTemporaryCollection.Invoke(guid, idx);

                _collectionIds[mannequinId] = guid;
            }

            if (_collectionIds.TryGetValue(mannequinId, out var collGuid))
            {
                var modTag = $"VenueSync_{mannequinId}_{modId}";
                _penumbraRemoveTemporaryMod.Invoke(modTag, collGuid, 1);

                _penumbraAddTemporaryMod.Invoke(modTag, collGuid, fileList, manipulationData, 1);
                VenueSync.Log.Debug($"Successfully loaded mod '{modLogName}' into collection {collName}");
            }
            else
            {
                VenueSync.Log.Error($"Failed to find collection for mannequin {mannequinId} after creation.");
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Failed to load mod '{modLogName}' for mannequin {mannequinId}: {exception.Message}");
        }
    }

    private void SetupMods(MannequinItem mannequin, KeyValuePair<ActorIdentifier, ActorData> mannequinActor, List<MannequinModItem> mods)
    {
        if (mods.Count == 0) return;

        var idx = mannequinActor.Value.Objects[0].Index.Index;

        List<EquipSlot> availableSlots = [EquipSlot.Head, EquipSlot.Body, EquipSlot.Hands, EquipSlot.Legs, EquipSlot.Feet];

        foreach (var mod in mods)
        {
            var isModEnabled = _configuration.AutoloadMods 
                ? !_venueSettings.InactiveMods.Contains(mod.id)
                : _venueSettings.ActiveMods.Contains(mod.id);

            if (!isModEnabled)
            {
                VenueSync.Log.Debug($"Skipping mod {mod.name} - mod is disabled");
                continue;
            }

            if (_failedMods.Contains(mod.id))
            {
                VenueSync.Log.Debug($"Skipping failed mod {mod.name} until manual reload");
                continue;
            }

            if (mod.file is "" or null || string.IsNullOrEmpty(mod.extension))
            {
                VenueSync.Log.Warning($"Mod validation failed for {mod.name}: missing file and/or extension, skipping until manual reload");
                MarkModFailed(mod.id);
                continue;
            }

            if (!_syncFileService.VerifyModFiles([mod]))
            {
                VenueSync.Log.Debug($"Mod files missing for {mod.name}, downloading...");
                
                _syncFileService.DownloadModFiles([mod], (modId, success) =>
                {
                    if (!success)
                    {
                        VenueSync.Log.Warning($"Failed to download mod files for {mod.name}, skipping until manual reload");
                        MarkModFailed(modId);
                    }
                    else
                    {
                        _hasQueuedReload = true;
                    }
                });
                
                continue;
            }

            try
            {
                if (availableSlots.Count == 0)
                {
                    VenueSync.Log.Warning($"No free equip slots left for mannequin {mannequin.name}, skipping mod {mod.name}");
                    continue;
                }

                var slot = availableSlots[0];
                availableSlots.RemoveAt(0);

                var manipulationData = _manipulationDataManager.BuildManipulationDataForSlot(mannequinActor, slot);

                VenueSync.Log.Debug($"Setting up mod {mod.name} for {mannequin.name} on slot {slot}");

                var fileList = _syncFileService.BuildModFileList(manipulationData.Path, mod);
                LoadMod(mannequin.id, mod.id, idx, fileList, manipulationData.ManipulationString, mod.name);
            }
            catch (Exception exception)
            {
                VenueSync.Log.Error($"Failed to setup mod {mod.name}: {exception}");
                MarkModFailed(mod.id);
            }
        }
    }

    private void RemoveCollection(string mannequinId)
    {
        if (_collectionIds.TryGetValue(mannequinId, out var guid))
        {
            _penumbraRemoveTemporaryCollection.Invoke(guid);
        }
    }
    
    private void MarkModFailed(string modId)
    {
        _failedMods.Add(modId);
        _stateService.VenueState.failed_mods.Add(modId);

        if (_configuration.AutoloadMods)
        {
            if (!_venueSettings.InactiveMods.Contains(modId))
                _venueSettings.InactiveMods.Add(modId);
        }
        else
        {
            _venueSettings.ActiveMods.Remove(modId);
        }

        _venueSettings.Save();
    }

    private async Task LoadTextureAsync(string imagePath, Action disposeExistingTexture, Func<Stream, Task> assignTextureFromStream, string successLogMessage, string errorContext)
    {
        try
        {
            disposeExistingTexture?.Invoke();

            await using var fileStream = File.OpenRead(imagePath);
            await assignTextureFromStream(fileStream);

            VenueSync.Log.Debug(successLogMessage);
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Failed to load {errorContext}: {ex.Message}");
        }
    }

    private async Task LoadLogoTextureAsync(string imagePath)
    {
        await LoadTextureAsync(
            imagePath,
            () => _stateService.VenueState.logoTexture?.Dispose(),
            async fileStream => _stateService.VenueState.logoTexture = await _textureProvider.CreateFromImageAsync(
                fileStream,
                leaveOpen: false,
                cancellationToken: CancellationToken.None
            ),
            "Logo texture loaded successfully",
            "logo texture"
        );
    }

    private async Task LoadStreamLogoTextureAsync(VenueStream stream, string imagePath)
    {
        await LoadTextureAsync(
            imagePath,
            () => stream.logoTexture?.Dispose(),
            async fileStream => stream.logoTexture = await _textureProvider.CreateFromImageAsync(
                fileStream,
                leaveOpen: false,
                cancellationToken: CancellationToken.None
            ),
            $"Stream logo texture loaded for {stream.name} from '{imagePath}'",
            $"stream logo texture for {stream.name} (path='{imagePath}')"
        );
    }

    private void DisposeStreamTextures()
    {
        try
        {
            var count = 0;
            foreach (var s in _stateService.VenueState.streams)
            {
                if (s.logoTexture != null)
                {
                    count++;
                    s.logoTexture.Dispose();
                }
                s.logoTexture = null;
            }

            VenueSync.Log.Debug($"Disposed {count} stream logo textures.");
        }
        catch (Exception ex)
        {
            VenueSync.Log.Debug($"Error disposing stream textures: {ex.Message}");
        }
    }

    private void QueueLogoDownloadsForCurrentVenue()
    {
        var vs = _stateService.VenueState;

        _syncFileService.MaybeDownloadFile(
            vs.id,
            vs.logo,
            "png",
            vs.hash,
            path => _ = Task.Run(async () =>
            {
                if (path == null) return;
                await LoadLogoTextureAsync(path);
            })
        );

        VenueSync.Log.Debug($"Processing streams: count={vs.streams.Count}, active='{vs.active_stream}'");
        foreach (var stream in vs.streams)
        {
            if (string.IsNullOrEmpty(stream.logo))
            {
                VenueSync.Log.Debug($"Stream '{stream.name}' has no logo URL; skipping download.");
                continue;
            }

            var fileId = $"{vs.id}_stream_{stream.id}";
            VenueSync.Log.Debug($"Queueing logo download for stream '{stream.name}': url='{stream.logo}', fileId='{fileId}'");
            _syncFileService.MaybeDownloadFile(
                fileId,
                stream.logo,
                "png",
                stream.hash,
                path => _ = Task.Run(async () =>
                {
                    if (path == null)
                    {
                        VenueSync.Log.Warning($"Download for stream '{stream.name}' returned null path (url='{stream.logo}').");
                        return;
                    }

                    VenueSync.Log.Debug($"Download for stream '{stream.name}' completed: path='{path}'. Starting texture load...");
                    await LoadStreamLogoTextureAsync(stream, path);
                })
            );
        }
    }

    private void OnVenueEntered(VenueState data)
    {
        DisposeMannequins();
        DisposeStreamTextures();
        _failedMods.Clear();
        _stateService.VenueState.failed_mods.Clear();

        UpdateVenueState(data);
        _stateService.VenueState.location.mods.Clear();

        QueueLogoDownloadsForCurrentVenue();

        VenueSync.Messager.NotificationMessage($"Welcome to [{data.name}]", NotificationType.Success);

        if (!_windowSystem.VenueWindowOpened())
        {
            _windowSystem.ToggleVenueWindow();
        }
    }

    private void OnVenueMod(VenueModData data)
    {
        if (_stateService.VenueState.id != data.venue_id || _stateService.VenueState.location.id != data.location_id)
        {
            return;
        }

        var mods = _stateService.VenueState.location.mods;

        var existingIndex = mods.FindIndex(m => string.Equals(m.id, data.mod.id, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            mods[existingIndex] = data.mod;
            VenueSync.Log.Debug($"Updated mod {data.mod.name} ({data.mod.id}) for location {data.location_id}");
        }
        else
        {
            mods.Add(data.mod);
            VenueSync.Log.Debug($"Added mod {data.mod.name} ({data.mod.id}) for location {data.location_id}");
        }

        _failedMods.Remove(data.mod.id);
        _stateService.VenueState.failed_mods.Remove(data.mod.id);

        ReloadAllMods();
    }

    private void OnVenueUpdated(VenueState data)
    {
        var preservedMods = new List<MannequinModItem>(_stateService.VenueState.location.mods);

        _failedMods.Clear();
        _stateService.VenueState.failed_mods.Clear();

        DisposeStreamTextures();
        UpdateVenueState(data);

        QueueLogoDownloadsForCurrentVenue();

        _stateService.VenueState.location.mods.Clear();
        _stateService.VenueState.location.mods.AddRange(preservedMods);
    }

    private void UpdateVenueState(VenueState venue)
    {
        _stateService.VenueState.id = venue.id;
        _stateService.VenueState.name = venue.name;
        _stateService.VenueState.location = venue.location;
        _stateService.VenueState.logo = venue.logo;
        _stateService.VenueState.hash = venue.hash;
        _stateService.VenueState.description = venue.description;
        _stateService.VenueState.open_hours = venue.open_hours;
        _stateService.VenueState.discord_invite = venue.discord_invite;
        _stateService.VenueState.carrd_url = venue.carrd_url;
        _stateService.VenueState.staff = venue.staff;
        _stateService.VenueState.tags = venue.tags;
        _stateService.VenueState.streams = venue.streams;
        _stateService.VenueState.active_stream = venue.active_stream;
        
        _venueSettings.Load();

        var streamCount = _stateService.VenueState.streams?.Count ?? 0;
        var withLogo = _stateService.VenueState.streams?.Count(s => !string.IsNullOrEmpty(s.logo)) ?? 0;
        VenueSync.Log.Debug($"UpdateVenueState: streams={streamCount}, withLogo={withLogo}, active='{_stateService.VenueState.active_stream}'");
    }

    private void OnVenueExited(string id)
    {
        DisposeMods();
        _stateService.VenueState.logoTexture?.Dispose();
        DisposeStreamTextures();
        
        if (_windowSystem.VenueWindowOpened())
        {
            _windowSystem.ToggleVenueWindow();
        }
    }

    private void OnServiceDisconnected()
    {
        DisposeMods();
        _stateService.VenueState.logoTexture?.Dispose();
        DisposeStreamTextures();
        _stateService.ResetVenueState();
    }
}
