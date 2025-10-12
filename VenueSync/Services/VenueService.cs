using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using VenueSync.Events;
using VenueSync.Services.IPC;
using VenueSync.Ui;

namespace VenueSync.Services;

public class VenueService: IDisposable
{
    private readonly IFramework _framework;
    private readonly ActorObjectManager _objects;
    private readonly StateService _stateService;
    private readonly VenueWindow _venueWindow;
    private readonly PenumbraIPC _penumbraIPC;
    private readonly VenueEntered _venueEntered;
    private readonly VenueExited _venueExited;
    
    private readonly AddTemporaryMod _penumbraAddTemporaryMod;
    private readonly RemoveTemporaryMod _penumbraRemoveTemporaryMod;
    private readonly CreateTemporaryCollection _penumbraCreateNamedTemporaryCollection;
    private readonly AssignTemporaryCollection _penumbraAssignTemporaryCollection;
    private readonly DeleteTemporaryCollection _penumbraRemoveTemporaryCollection;
    
    private Dictionary<string, Guid> _collectionIds;
    private bool _running = false;
    private bool _updateMannequins = false;
    private bool _clearMannequins = false;
    
    public VenueService(IDalamudPluginInterface pluginInterface, IFramework framework, ActorObjectManager objects,
        StateService stateService, VenueWindow venueWindow, PenumbraIPC penumbraIPC,
        VenueEntered @venueEntered, VenueExited @venueExited)
    {
        _framework = framework;
        _objects = objects;
        _stateService = stateService;
        _venueWindow = venueWindow;
        _penumbraIPC = penumbraIPC;
        _venueEntered = @venueEntered;
        _venueExited = @venueExited;

        _collectionIds = [];
        
        _penumbraAddTemporaryMod = new AddTemporaryMod(pluginInterface);
        _penumbraRemoveTemporaryMod = new RemoveTemporaryMod(pluginInterface);
        _penumbraCreateNamedTemporaryCollection = new CreateTemporaryCollection(pluginInterface);
        _penumbraAssignTemporaryCollection = new AssignTemporaryCollection(pluginInterface);
        _penumbraRemoveTemporaryCollection = new DeleteTemporaryCollection(pluginInterface);
        
        _framework.Update += OnFrameworkUpdate;
        
        _venueEntered.Subscribe(OnVenueEntered, VenueEntered.Priority.High);
        _venueExited.Subscribe(OnVenueExited, VenueExited.Priority.High);
    }

    public void Dispose()
    {
        DisposeMannequins();
        _venueEntered.Unsubscribe(OnVenueEntered);
        _venueExited.Unsubscribe(OnVenueExited);
        _framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_running) {
            VenueSync.Log.Warning("Skipping processing venue while already running.");
            return;
        }
        _running = true;
        
        try
        {
            if (_penumbraIPC.IsAvailable)
            {
                if (_updateMannequins)
                {
                    HandleMannequins();
                }
                if (_clearMannequins)
                {
                    DisposeMannequins();
                }
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"VenueSync Failed during framework update (venue): {exception.Message}");
        }
        
        _running = false;
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

            if (loadedMannequin != null)
            {
                SetupMod(loadedMannequin.id, mannequin.Value.Objects[0].Index.Index);
            }
        }

        _updateMannequins = false;
    }
    
    private void DisposeMannequins()
    {
        foreach (var idSet in _collectionIds)
        {
            RemoveMod(idSet.Key);
        }
        
        _collectionIds = [];
        _clearMannequins = false;
    }

    private void SetupMod(string uid, ushort idx)
    {
        try
        {
            Dictionary<string, string> fileList = new Dictionary<string, string> {
                {
                    "chara/equipment/e0234/vfx/eff/ve0001.avfx", @"D:\FFXIVVenueSync\0199d5b2-9c36-72e3-94d3-c4c72ada6a4d\vm0001.avfx"
                }
            };
            string manipulationData = "H4sIAAAAAAAACjWOMQ+CMBSE/S03d0Bw6qaRgYFogroYhwo1qdBXrI8oMfx3Q63Tu3y5e3eL8weHsdeQKGwNgVKR6YdOsXEE+UFO7MdZlIq1N6orGsilwFbXP50InG7vSP+mNRkbXkTHmtmb68C6VM8WcpmkmUDlBmpCcBLYXe+65jglfwymt5oYAntvrPLj7EuzlUCla0dNJHO58kYRh/qQqzrHkNi4ZoQIJ5IjteRehGm6fAFKnKXa9wAAAA==";

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
        
        _venueWindow.Toggle();

        _updateMannequins = true;
    }

    private void OnVenueExited()
    {
        _clearMannequins = true;
        
        if (_venueWindow.IsOpen)
        {
            _venueWindow.Toggle();
        }
    }
}
