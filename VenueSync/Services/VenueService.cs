using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
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
    private readonly GameStateService _gameStateService;
    private readonly StateService _stateService;
    private readonly VenueWindow _venueWindow;
    private readonly PenumbraIPC _penumbraIPC;
    private readonly ManipulationDataManager _manipulationDataManager;
    private readonly VenueEntered _venueEntered;
    private readonly VenueExited _venueExited;
    private readonly LoggedIn _loggedIn;
    private readonly LoggedOut _loggedOut;
    
    private readonly AddTemporaryMod _penumbraAddTemporaryMod;
    private readonly RemoveTemporaryMod _penumbraRemoveTemporaryMod;
    private readonly CreateTemporaryCollection _penumbraCreateNamedTemporaryCollection;
    private readonly AssignTemporaryCollection _penumbraAssignTemporaryCollection;
    private readonly DeleteTemporaryCollection _penumbraRemoveTemporaryCollection;
    
    private Dictionary<string, Guid> _collectionIds;
    
    public VenueService(IDalamudPluginInterface pluginInterface, ActorObjectManager objects, 
        Configuration configuration, GameStateService gameStateService,
        StateService stateService, VenueWindow venueWindow, PenumbraIPC penumbraIPC, ManipulationDataManager manipulationDataManager,
        VenueEntered @venueEntered, VenueExited @venueExited, LoggedIn @loggedIn, LoggedOut @loggedOut)
    {
        _configuration = configuration;
        _gameStateService = gameStateService;
        _objects = objects;
        _stateService = stateService;
        _venueWindow = venueWindow;
        _penumbraIPC = penumbraIPC;
        _manipulationDataManager = manipulationDataManager;
        _venueEntered = @venueEntered;
        _venueExited = @venueExited;
        _loggedIn = @loggedIn;
        _loggedOut = @loggedOut;

        _collectionIds = [];
        
        VenueSync.Log.Debug("Starting Venue Service");
        
        _penumbraAddTemporaryMod = new AddTemporaryMod(pluginInterface);
        _penumbraRemoveTemporaryMod = new RemoveTemporaryMod(pluginInterface);
        _penumbraCreateNamedTemporaryCollection = new CreateTemporaryCollection(pluginInterface);
        _penumbraAssignTemporaryCollection = new AssignTemporaryCollection(pluginInterface);
        _penumbraRemoveTemporaryCollection = new DeleteTemporaryCollection(pluginInterface);
        
        _venueEntered.Subscribe(OnVenueEntered, VenueEntered.Priority.High);
        _venueExited.Subscribe(OnVenueExited, VenueExited.Priority.High);
        _loggedOut.Subscribe(DisposeMods, LoggedOut.Priority.High);
    }

    public void ReloadMods()
    {
        _gameStateService.RunOnFrameworkThread(() =>
        {
            if (_penumbraIPC.IsAvailable && _gameStateService.IsLoggedIn)
            {
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
        DisposeMannequins();
        _venueEntered.Unsubscribe(OnVenueEntered);
        _venueExited.Unsubscribe(OnVenueExited);
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
            //SetupMod(loadedMannequin, mannequin, mods);
        }
    }
    
    private void DisposeMannequins()
    {
        foreach (var idSet in _collectionIds)
        {
            RemoveMod(idSet.Key);
        }
        
        _collectionIds = [];
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
            /*if (!_fileService.VerifyModFiles(mods))
            {
                _fileService.DownloadModFiles(mods);
                return;
            }*/
            
            /*LoadMod(uid, idx, _fileService.BuildModFileList(manipulationData.Paths, mods), manipulationData.ManipulationString);*/
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Failed to setup mod: {exception.Message}");
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

    private void OnVenueEntered(VenueEnteredData data)
    {
        _stateService.VenueState.id = data.venue.id;
        _stateService.VenueState.name = data.venue.name;
        _stateService.VenueState.location = data.location;
        
        VenueSync.Messager.NotificationMessage($"Welcome to [{data.venue.name}]", NotificationType.Success);

        if (!_venueWindow.IsOpen)
        {
            _venueWindow.Toggle();
        }
    }

    private void OnVenueExited()
    {
        DisposeMods();
        if (_venueWindow.IsOpen)
        {
            _venueWindow.Toggle();
        }
    }
}
