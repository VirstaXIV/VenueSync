using System;
using System.IO;
using Newtonsoft.Json;
using VenueSync.Services;
using VenueSync.Ui;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace VenueSync;

public class EphemeralConfig : ISavable
{
    public int Version { get; set; } = Configuration.Constants.CurrentVersion;
    public MainWindow.TabType SelectedTab { get; set; } = MainWindow.TabType.Settings;

    [JsonIgnore] private readonly SaveService _saveService;

    public EphemeralConfig(SaveService saveService)
    {
        _saveService = saveService;
        Load();
    }

    public void Load()
    {
        static void HandleDeserializationError(object? sender, ErrorEventArgs errorArgs)
        {
            VenueSync.Log.Error(
                $"Error parsing ephemeral Configuration at {errorArgs.ErrorContext.Path}");
            errorArgs.ErrorContext.Handled = true;
        }

        if (!File.Exists(_saveService.FileNames.EphemeralConfigFile))
            return;

        try
        {
            var text = File.ReadAllText(_saveService.FileNames.EphemeralConfigFile);
            JsonConvert.PopulateObject(text, this, new JsonSerializerSettings {
                Error = HandleDeserializationError,
            });
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Error ephemeral Configuration: {ex.Message}");
        }
    }

    public string ToFilename(FilenameManager fileNames) => fileNames.EphemeralConfigFile;

    public void Save(StreamWriter writer)
    {
        using var jWriter = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        var serializer = new JsonSerializer {
            Formatting = Formatting.Indented
        };
        serializer.Serialize(jWriter, this);
    }

    public void Save() => _saveService.DelaySave(this, TimeSpan.FromSeconds(5));
}
