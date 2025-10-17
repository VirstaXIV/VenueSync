using System.IO;
using Dalamud.Plugin;
using OtterGui.Classes;
using OtterGui.Log;

namespace VenueSync.Services;

public class FilenameManager
{
    private readonly StateService _stateService;
    
    public readonly string ConfigDirectory;
    public readonly string ConfigFile;

    public FilenameManager(IDalamudPluginInterface pluginInterface, StateService stateService)
    {
        ConfigDirectory = pluginInterface.ConfigDirectory.FullName;
        ConfigFile = pluginInterface.ConfigFile.FullName;
        
        _stateService = stateService;
    }

    public string GetVenueConfigFile()
    {
        return Path.Combine(ConfigDirectory, $"venue-{_stateService.VenueState.id}.json");
    }
}

public interface ISavable : ISavable<FilenameManager>
{ }

public sealed class SaveService: SaveServiceBase<FilenameManager>
{
    public SaveService(Logger log, FrameworkManager framework, FilenameManager fileNames) : base(log, framework, fileNames)
    { }
}