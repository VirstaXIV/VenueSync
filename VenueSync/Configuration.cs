using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Configuration;
using Newtonsoft.Json;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;
using VenueSync.Services;

namespace VenueSync;

public class Configuration : IPluginConfiguration, ISavable
{
    public bool OpenWindowAtStart { get; set; } = false;
    public bool ClientMode { get; set; } = true;
    
    public bool EnableDtrBar { get; set; } = true;
    public bool AutoConnect { get; set; } = true;
    public bool NotifyEntrances { get; set; } = false;
    public bool AutoloadMods { get; set; } = false;
    public string ServerToken { get; set; } = string.Empty;
    public string ServerUserID { get; set; } = string.Empty;

    public int Version { get; set; } = Constants.CurrentVersion;
    public string SyncFolder { get; set; } = string.Empty;

    // Storage management settings
    public long MaxStorageSizeBytes { get; set; } = 10L * 1024 * 1024 * 1024; // 10 GB default
    public int FileRetentionDays { get; set; } = 30; // Delete files not accessed in 30 days
    public bool AutoCleanupEnabled { get; set; } = true;

    [JsonIgnore] private readonly SaveService _saveService;

    public Configuration(SaveService saveService)
    {
        _saveService = saveService;
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
        
#if DEBUG
        public const string VenueSyncDashboard = "https://venuesync.test";
        public const string API_ENDPOINT = "https://venuesync.test/api";
        public const string SOCKET_APP_KEY = "laravel-herd";
        public const string SOCKET_HOST = "localhost";
        public const int SOCKET_PORT = 8080;
        public const string SOCKET_SCHEME = "http";
#else
        public const string VenueSyncDashboard = "https://dev.xivvenuesync.com";
        public const string API_ENDPOINT = "https://dev.xivvenuesync.com/api";
        public const string SOCKET_APP_KEY = "omgsf8gip6kbwtb0oqju";
        public const string SOCKET_HOST = "ws.dev.xivvenuesync.com";
        public const int SOCKET_PORT = 443;
        public const string SOCKET_SCHEME = "https";
#endif

    }
}
