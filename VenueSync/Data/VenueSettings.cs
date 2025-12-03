using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using VenueSync.Services;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace VenueSync.Data;

public class VenueSettings: ISavable
{
    [JsonIgnore]
    private readonly SaveService _saveService;
    [JsonIgnore]
    private readonly StateService _stateService;
    [JsonIgnore] private string _lastConfig = string.Empty;
    
    public List<string> ActiveMods { get; set; } = new();
    public List<string> InactiveMods { get; set; } = new();

    public VenueSettings(SaveService saveService, StateService stateService)
    {
        _saveService = saveService;
        _stateService = stateService;
    }
    
    public void Load()
    {
        _lastConfig = _saveService.FileNames.GetVenueConfigFile();
        
        static void HandleDeserializationError(object? sender, ErrorEventArgs errorArgs)
        {
            VenueSync.Log.Error($"Error checking Venue Config: {errorArgs.ErrorContext.Path} [{errorArgs.ErrorContext.Error}]");
            errorArgs.ErrorContext.Handled = true;
        }

        if (_stateService.VenueState.id == string.Empty || !File.Exists(_lastConfig))
            return;

        try
        {
            var text = File.ReadAllText(_lastConfig);
            JsonConvert.PopulateObject(text, this, new JsonSerializerSettings
            {
                Error = HandleDeserializationError,
            });

            if (SanitizeForCurrentVenue())
            {
                Save();
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Error checking Venue Config: {_saveService.FileNames.GetVenueConfigFile()} [{exception.Message}]");
        }
    }

    public void Save()
    {
        if (_stateService.VenueState.id != string.Empty)
        {
            SanitizeForCurrentVenue();
            _saveService.DelaySave(this, TimeSpan.FromSeconds(5));
        }
    }

    private bool SanitizeForCurrentVenue()
    {
        try
        {
            HashSet<string>? validIds = null;
            var venue = _stateService.VenueState;
            if (venue != null && venue.location != null && venue.location.mods != null && venue.location.mods.Count > 0)
            {
                validIds = new HashSet<string>(venue.location.mods.Select(m => m.id), StringComparer.OrdinalIgnoreCase);
            }

            var beforeActive = ActiveMods.ToList();
            var beforeInactive = InactiveMods.ToList();

            IEnumerable<string> Normalize(IEnumerable<string> src) =>
                src.Where(s => !string.IsNullOrWhiteSpace(s))
                   .Select(s => s.Trim())
                   .Distinct(StringComparer.OrdinalIgnoreCase);

            var newActive = Normalize(ActiveMods);
            var newInactive = Normalize(InactiveMods);

            if (validIds != null)
            {
                newActive = newActive.Where(validIds.Contains);
                newInactive = newInactive.Where(validIds.Contains);
            }

            ActiveMods = newActive.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
            InactiveMods = newInactive.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();

            bool changed = !beforeActive.SequenceEqual(ActiveMods) || !beforeInactive.SequenceEqual(InactiveMods);
            return changed;
        }
        catch
        {
            return false;
        }
    }
    
    public string ToFilename(FilenameManager fileNames)
        => _lastConfig;
    
    public void Save(StreamWriter writer)
    {
        using var jWriter    = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        var       serializer = new JsonSerializer { Formatting         = Formatting.Indented };
        serializer.Serialize(jWriter, this);
    }
}
