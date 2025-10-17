using System;
using System.Collections.Generic;
using System.IO;
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
            _saveService.DelaySave(this, TimeSpan.FromSeconds(5));
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
