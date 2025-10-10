using System;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using OtterGui.Services;
using VenueSync.Ui;

namespace VenueSync.Services;

public class CommandService: IDisposable, IApiService
{
    private const string MainCommandString  = "/venuesync";
    
    private readonly ICommandManager _commands;
    private readonly MainWindow _mainWindow;
    private readonly Configuration _configuration;

    public CommandService(ICommandManager commands, MainWindow mainWindow, Configuration configuration)
    {
        _commands = commands;
        _mainWindow = mainWindow;
        _configuration = configuration;
        
        _commands.AddHandler(MainCommandString, new CommandInfo(OnVenueSync) { HelpMessage = "Open or close the VenueSync window." });
    }
    
    public void Dispose()
    {
        _commands.RemoveHandler(MainCommandString);
    }

    private void OnVenueSync(string command, string arguments)
    {
        _mainWindow.Toggle();
    }
}
