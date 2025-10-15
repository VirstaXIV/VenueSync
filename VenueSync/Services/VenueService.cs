using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Textures.TextureWraps;
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

public class VenueService: IDisposable
{
    private readonly Configuration _configuration;
    private readonly ActorObjectManager _objects;
    private readonly ITextureProvider _textureProvider;
    private readonly GameStateService _gameStateService;
    private readonly StateService _stateService;
    private readonly SyncFileService _syncFileService;
    private readonly VenueSyncWindowSystem _windowSystem;
    private readonly PenumbraIPC _penumbraIPC;
    private readonly ManipulationDataManager _manipulationDataManager;
    private readonly VenueEntered _venueEntered;
    private readonly VenueUpdated _venueUpdated;
    private readonly VenueExited _venueExited;
    private readonly LoggedIn _loggedIn;
    private readonly LoggedOut _loggedOut;
    private readonly ReloadMods _reloadMods;
    private readonly DisableMods _disableMods;
    
    private readonly AddTemporaryMod _penumbraAddTemporaryMod;
    private readonly RemoveTemporaryMod _penumbraRemoveTemporaryMod;
    private readonly CreateTemporaryCollection _penumbraCreateNamedTemporaryCollection;
    private readonly AssignTemporaryCollection _penumbraAssignTemporaryCollection;
    private readonly DeleteTemporaryCollection _penumbraRemoveTemporaryCollection;
    private readonly RedrawObject _penumbraRedrawObject;
    
    private Dictionary<string, Guid> _collectionIds;
    
    public VenueService(IDalamudPluginInterface pluginInterface, ActorObjectManager objects, ITextureProvider textureProvider,
        Configuration configuration, GameStateService gameStateService, SyncFileService syncFileService,
        StateService stateService, VenueSyncWindowSystem windowSystem, PenumbraIPC penumbraIPC, ManipulationDataManager manipulationDataManager,
        VenueEntered @venueEntered, VenueUpdated @venueUpdated, VenueExited @venueExited, LoggedIn @loggedIn, LoggedOut @loggedOut, ReloadMods @reloadMods, DisableMods @disableMods)
    {
        _configuration = configuration;
        _gameStateService = gameStateService;
        _objects = objects;
        _textureProvider = textureProvider;
        _stateService = stateService;
        _syncFileService = syncFileService;
        _windowSystem = windowSystem;
        _penumbraIPC = penumbraIPC;
        _manipulationDataManager = manipulationDataManager;
        _venueEntered = @venueEntered;
        _venueUpdated = @venueUpdated;
        _venueExited = @venueExited;
        _loggedIn = @loggedIn;
        _loggedOut = @loggedOut;
        _reloadMods = @reloadMods;
        _disableMods = @disableMods;

        _collectionIds = [];
        
        VenueSync.Log.Debug("Starting Venue Service");
        
        _penumbraAddTemporaryMod = new AddTemporaryMod(pluginInterface);
        _penumbraRemoveTemporaryMod = new RemoveTemporaryMod(pluginInterface);
        _penumbraCreateNamedTemporaryCollection = new CreateTemporaryCollection(pluginInterface);
        _penumbraAssignTemporaryCollection = new AssignTemporaryCollection(pluginInterface);
        _penumbraRemoveTemporaryCollection = new DeleteTemporaryCollection(pluginInterface);
        _penumbraRedrawObject = new RedrawObject(pluginInterface);
        
        _venueEntered.Subscribe(OnVenueEntered, VenueEntered.Priority.High);
        _venueUpdated.Subscribe(OnVenueUpdated, VenueUpdated.Priority.High);
        _venueExited.Subscribe(OnVenueExited, VenueExited.Priority.High);
        _loggedOut.Subscribe(DisposeMods, LoggedOut.Priority.High);
        _reloadMods.Subscribe(ReloadAllMods, ReloadMods.Priority.High);
        _disableMods.Subscribe(DisposeMods, DisableMods.Priority.High);
    }

    public void ReloadAllMods()
    {
        _gameStateService.RunOnFrameworkThread(() =>
        {
            if (_penumbraIPC.IsAvailable && _gameStateService.IsLoggedIn)
            {
                DisposeMannequins();
                HandleMannequins();
            }
        }).ConfigureAwait(false);
    }
    
    public void DisposeMods()
    {
        _gameStateService.RunOnFrameworkThread(() =>
        {
            if (_penumbraIPC.IsAvailable)
            {
                DisposeMannequins();
            }
        }).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _stateService.VenueState.logoTexture?.Dispose();
        DisposeMannequins();
        _venueEntered.Unsubscribe(OnVenueEntered);
        _venueExited.Unsubscribe(OnVenueExited);
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
            List<MannequinModItem> mods = _stateService.VenueState.location.mods.Where(m => m.mannequin_id.Equals(loadedMannequin.id)).ToList();
            SetupMod(loadedMannequin, mannequin, mods);
        }

        Redraw();
    }
    
    private void DisposeMannequins()
    {
        foreach (var idSet in _collectionIds)
        {
            RemoveMod(idSet.Key);
        }
        
        _collectionIds = [];
        
        Redraw();
    }

    private void LoadMod(string uid, ushort idx, Dictionary<string, string> fileList, string manipulationData)
    {
        try
        {
            var collName = "VenueSync_" + uid;

            Guid guid;
            if (_collectionIds.TryGetValue(uid, out var id))
            {
                guid = id;
            }
            else
            {
                VenueSync.Log.Debug($"Creating Temp Collection: {collName}");

                PenumbraApiEc penEc = _penumbraCreateNamedTemporaryCollection.Invoke(uid, collName, out guid);

                if (penEc != PenumbraApiEc.Success)
                {
                    VenueSync.Log.Error($"Failed to create temporary collection for {collName} with error code {penEc}. Please include this line in any error reports");
                    return;
                }

                VenueSync.Log.Debug($"Created Temp Collection: {collName}");
            }

            VenueSync.Log.Debug($"Assigning Temp Collection: {idx}");
            VenueSync.Log.Debug($"Creating Temp Mod");

            try
            {
                _penumbraAssignTemporaryCollection.Invoke(guid, idx, true);
                _penumbraRemoveTemporaryMod.Invoke("VenueSync", guid, 0);
                _penumbraAddTemporaryMod.Invoke("VenueSync", guid, fileList, manipulationData, 0);
            }
            catch (Exception exception)
            {
                VenueSync.Log.Error($"Failed to create mod: {exception.Message}");
            }

            VenueSync.Log.Debug($"Assigned Temp Collection: {idx}");
            VenueSync.Log.Debug($"Created Temp Mod");

            _collectionIds[uid] = guid;
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Failed to create temp mod: {exception.Message}");
        }
    }

    private void SetupMod(MannequinItem mannequin, KeyValuePair<ActorIdentifier, ActorData> mannequinActor, List<MannequinModItem> mods)
    {
        // User must have manually selected to enable.
        if (!_configuration.ActiveMods.Contains(mannequin.id))
        {
            return;
        }
        try
        {
            ushort idx = mannequinActor.Value.Objects[0].Index.Index;
            string uid = mannequin.id;

            ManipulationDataRecord manipulationData = _manipulationDataManager.BuildManipulationData(mannequinActor, mods);

            // Check files are downloaded and ready, or ensure download is running
            if (!_syncFileService.VerifyModFiles(mods))
            {
                _syncFileService.DownloadModFiles(mods);
                return;
            }
            
            VenueSync.Log.Debug($"Setting Meta for Mod: {manipulationData.ManipulationString}");
            
            LoadMod(uid, idx, _syncFileService.BuildModFileList(manipulationData.Paths, mods), manipulationData.ManipulationString);
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Failed to setup mod: {exception.ToString()}");
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
    
    async private Task LoadLogoTexture(string imagePath)
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
        _stateService.VenueState.id = data.venue.id;
        _stateService.VenueState.name = data.venue.name;
        _stateService.VenueState.location = data.location;
        _stateService.VenueState.logo = data.venue.logo;
        _stateService.VenueState.hash = data.venue.hash;
        _stateService.VenueState.description = data.venue.description;
        _stateService.VenueState.open_hours = data.venue.open_hours;
        _stateService.VenueState.discord_invite = data.venue.discord_invite;
        _stateService.VenueState.staff = data.staff;
        _stateService.VenueState.tags = data.tags;
        _stateService.VenueState.streams = data.streams;

        _syncFileService.MaybeDownloadFile(data.venue.id, data.venue.logo, "png", data.venue.hash, path =>
        {
            _ = Task.Run(async () => await LoadLogoTexture(path));
        });
        
        VenueSync.Messager.NotificationMessage($"Welcome to [{data.venue.name}]", NotificationType.Success);

        if (!_windowSystem.VenueWindowOpened())
        {
            _windowSystem.ToggleVenueWindow();
        }

        ReloadAllMods();
    }

    private void OnVenueUpdated(VenueUpdatedData data)
    {
        _stateService.VenueState.id = data.venue.id;
        _stateService.VenueState.name = data.venue.name;
        _stateService.VenueState.location = data.location;
        _stateService.VenueState.logo = data.venue.logo;
        _stateService.VenueState.hash = data.venue.hash;
        _stateService.VenueState.description = data.venue.description;
        _stateService.VenueState.open_hours = data.venue.open_hours;
        _stateService.VenueState.discord_invite = data.venue.discord_invite;
        _stateService.VenueState.staff = data.staff;
        _stateService.VenueState.tags = data.tags;
        _stateService.VenueState.streams = data.streams;
        
        _syncFileService.MaybeDownloadFile(data.venue.id, data.venue.logo, "png", data.venue.hash, path =>
        {
            _ = Task.Run(async () => await LoadLogoTexture(path));
        });
        
        ReloadAllMods();
    }

    private void OnVenueExited(string id)
    {
        DisposeMods();
        if (_windowSystem.VenueWindowOpened())
        {
            _windowSystem.ToggleVenueWindow();
        }
    }
}
