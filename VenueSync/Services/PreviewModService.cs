using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using System.Text.Json;
using VenueSync.Data;

namespace VenueSync.Services;

public class PreviewModService : IDisposable
{
    private readonly AddTemporaryMod _addTempMod;
    private readonly RemoveTemporaryMod _removeTempMod;
    private readonly CreateTemporaryCollection _createTempCollection;
    private readonly AssignTemporaryCollection _assignTempCollection;
    private readonly DeleteTemporaryCollection _deleteTempCollection;
    private readonly RedrawObject _redrawObject;
    private readonly ActorObjectManager _objects;
    private readonly ManipulationDataManager _manipulationDataManager;

    private Guid? _collectionId;
    private int? _actorIndex;
    private const string PreviewCollectionName = "VenueSync_ModPreview";
    private const string PreviewTag = "VenueSync_ModPreview_Tag";

    public PreviewModService(IDalamudPluginInterface pluginInterface, ActorObjectManager objects)
    {
        _addTempMod = new AddTemporaryMod(pluginInterface);
        _removeTempMod = new RemoveTemporaryMod(pluginInterface);
        _createTempCollection = new CreateTemporaryCollection(pluginInterface);
        _assignTempCollection = new AssignTemporaryCollection(pluginInterface);
        _deleteTempCollection = new DeleteTemporaryCollection(pluginInterface);
        _redrawObject = new RedrawObject(pluginInterface);
        _objects = objects;
        _manipulationDataManager = new ManipulationDataManager();
    }

    public void Dispose()
    {
        try { ClearPreview(); } catch { /* ignore */ }
    }

    private bool TryFindNearestMannequin(out KeyValuePair<ActorIdentifier, ActorData> mannequin, out ushort index)
    {
        // Pick the first mannequin-like actor (retainer) with a model loaded.
        mannequin = _objects
            .Where(p => p.Value.Objects.Any(a => a.Model))
            .FirstOrDefault(p => p.Key.Type is IdentifierType.Retainer);

        if (mannequin.Key == default || mannequin.Value.Objects.Count == 0)
        {
            index = 0;
            return false;
        }

        index = mannequin.Value.Objects[0].Index.Index;
        return true;
    }

    public bool ValidateFolder(string folder, out Dictionary<string, string> fileList, out string? error)
    {
        fileList = new Dictionary<string, string>();
        error = null;

        try
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                error = "Folder does not exist.";
                return false;
            }

            var defaultJson = Path.Combine(folder, "default_mod.json");
            if (!File.Exists(defaultJson))
            {
                error = "default_mod.json not found in selected folder.";
                return false;
            }

            var text = File.ReadAllText(defaultJson);
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("Files", out var filesElement) || filesElement.ValueKind != JsonValueKind.Object)
            {
                error = "default_mod.json missing 'Files' object.";
                return false;
            }

            foreach (var property in filesElement.EnumerateObject())
            {
                var targetPath = property.Name;
                if (property.Value.ValueKind != JsonValueKind.String)
                    continue;

                var localFile = property.Value.GetString() ?? string.Empty;

                if (!Path.IsPathRooted(localFile))
                    localFile = Path.Combine(folder, localFile);

                if (!File.Exists(localFile))
                    continue;

                if (!string.IsNullOrWhiteSpace(targetPath))
                {
                    fileList[targetPath] = localFile;
                }
            }

            if (fileList.Count == 0)
            {
                error = "No valid files in default_mod.json.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Validation error: {ex.Message}";
            return false;
        }
    }

    public bool Preview(Dictionary<string, string> fileList)
    {
        try
        {
            if (!TryFindNearestMannequin(out var mannequin, out var index))
            {
                VenueSync.Log.Warning("No mannequin found to preview on.");
                return false;
            }

            if (_collectionId == null)
            {
                VenueSync.Log.Debug($"Creating Temp Collection: {PreviewCollectionName}");

                var ec = _createTempCollection.Invoke("VenueSync_Preview", PreviewCollectionName, out var cid);
                if (ec != PenumbraApiEc.Success)
                {
                    VenueSync.Log.Error($"Failed to create preview collection: {ec}");
                    return false;
                }
                _collectionId = cid;
                _assignTempCollection.Invoke(cid, index);
            }
            else if (_actorIndex != index)
            {
                _assignTempCollection.Invoke(_collectionId.Value, index);
            }

            _actorIndex = index;

            var manipulationData = _manipulationDataManager.BuildManipulationDataForSlot(mannequin, EquipSlot.Body);

            // Remap the single .avfx (or top-level) entry to use the generated manipulation path
            var mapped = new Dictionary<string, string>(fileList);
            string? avfxKey = mapped.Keys.FirstOrDefault(k => k.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase));
            if (avfxKey == null)
            {
                avfxKey = mapped.FirstOrDefault(kv => kv.Value.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase)).Key;
            }
            if (avfxKey == null && mapped.Count > 0)
            {
                avfxKey = mapped.Keys.First();
            }
            if (avfxKey != null && mapped.TryGetValue(avfxKey, out var avfxValue))
            {
                mapped.Remove(avfxKey);
                mapped[manipulationData.Path] = avfxValue;
            }

            VenueSync.Log.Debug($"Created Temp Collection: {PreviewCollectionName}");
            _removeTempMod.Invoke(PreviewTag, _collectionId.Value, 10);
            _addTempMod.Invoke(PreviewTag, _collectionId.Value, mapped, manipulationData.ManipulationString, 10);
            VenueSync.Log.Debug($"Successfully loaded mod '{PreviewTag}' into collection {PreviewCollectionName}");

            _redrawObject.Invoke(index);
            return true;
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Preview failed: {ex.Message}");
            return false;
        }
    }

    public void ClearPreview()
    {
        try
        {
            if (_collectionId != null)
            {
                _removeTempMod.Invoke(PreviewTag, _collectionId.Value, 1);
                _deleteTempCollection.Invoke(_collectionId.Value);
            }
            if (_actorIndex != null)
            {
                _redrawObject.Invoke((int)_actorIndex);
            }
        }
        catch (Exception ex)
        {
            VenueSync.Log.Debug($"ClearPreview encountered an error: {ex.Message}");
        }
        finally
        {
            _collectionId = null;
            _actorIndex = null;
        }
    }
}
