using System;
using System.IO;
using Dalamud.Configuration;
using Newtonsoft.Json;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;
using VenueSync.Services;

namespace VenueSync;

public class Configuration : IPluginConfiguration, ISavable
{
    [JsonIgnore]
    public readonly EphemeralConfig Ephemeral;
    
    public bool OpenWindowAtStart { get; set; } = false;
    public bool AutoConnect { get; set; } = true;
    public string ServerToken { get; set; } = string.Empty;
    public string ServerUserID { get; set; } = string.Empty;

    public int Version { get; set; } = Constants.CurrentVersion;

    [JsonIgnore] private readonly SaveService _saveService;

    public Configuration(SaveService saveService, EphemeralConfig ephemeral)
    {
        _saveService = saveService;
        Ephemeral = ephemeral;
        Load();
    }

    public void Save() => _saveService.DelaySave(this);
    
    public void SaveNow() => _saveService.ImmediateSave(this);

    private void Load()
    {
        if (!File.Exists(_saveService.FileNames.ConfigFile))
            return;

        try
        {
            var text = File.ReadAllText(_saveService.FileNames.ConfigFile);
            JsonConvert.PopulateObject(text, this, new JsonSerializerSettings {
                Error = HandleDeserializationError,
            });
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Error reading Configuration: {ex.Message}");
        }

        static void HandleDeserializationError(object? sender, ErrorEventArgs errorArgs)
        {
            VenueSync.Log.Error($"Error parsing Configuration at {errorArgs.ErrorContext.Path}");
            errorArgs.ErrorContext.Handled = true;
        }
    }

    public string ToFilename(FilenameManager fileNames) => fileNames.ConfigFile;

    public void Save(StreamWriter writer)
    {
        using var jWriter = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        var serializer = new JsonSerializer {
            Formatting = Formatting.Indented
        };
        serializer.Serialize(jWriter, this);
    }
    
    public static class Constants
    {
        public const int CurrentVersion = 1;
        public const string XIVAuthEndpoint = "https://venuesync.test/api/auth/xivauth/register";
        public const string SOCKET_APP_KEY = "laravel-herd";
        public const string SOCKET_HOST = "localhost";
        public const string SOCKET_CHANNEL_AUTH = "https://venuesync.test/api/broadcasting/auth";
        public const string SOCKET_USER_AUTH = "https://venuesync.test/api/broadcasting/user-auth";
        public const int SOCKET_PORT = 8080;
        public const string SOCKET_SCHEME = "http";
        public const string LocationEndpoint = "https://venuesync.test/api/location/send";
        public const string MeEndpoint = "https://venuesync.test/api/me";
    }
}
