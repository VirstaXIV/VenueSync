using System.IO;
using Dalamud.Plugin;
using OtterGui.Classes;
using OtterGui.Log;

namespace VenueSync.Services;

public class FilenameManager
{
    public readonly string ConfigDirectory;
    public readonly string ConfigFile;
    public readonly string EphemeralConfigFile;

    public FilenameManager(IDalamudPluginInterface pluginInterface)
    {
        ConfigDirectory = pluginInterface.ConfigDirectory.FullName;
        ConfigFile = pluginInterface.ConfigFile.FullName;
        EphemeralConfigFile = Path.Combine(ConfigDirectory, "ephemeral_config.json");
    }
}

public interface ISavable : ISavable<FilenameManager>
{ }

public sealed class SaveService: SaveServiceBase<FilenameManager>
{
    public SaveService(Logger log, FrameworkManager framework, FilenameManager fileNames) : base(log, framework, fileNames)
    { }
}