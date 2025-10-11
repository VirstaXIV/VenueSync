using System;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace VenueSync.Ui;

public class VenueSyncWindowSystem: IDisposable
{
    private readonly WindowSystem _windowSystem = new("VenueSync");
    private readonly IUiBuilder _uiBuilder;
    private readonly MainWindow _mainWindow;

    public VenueSyncWindowSystem(IUiBuilder uiBuilder, MainWindow mainWindow, VenueWindow venueWindow, HouseVerifyWindow houseVerifyWindow)
    {
        _uiBuilder = uiBuilder;
        _mainWindow = mainWindow;
        _windowSystem.AddWindow(mainWindow);
        _windowSystem.AddWindow(venueWindow);
        _windowSystem.AddWindow(houseVerifyWindow);
        
        _uiBuilder.OpenMainUi += _mainWindow.Toggle;
        _uiBuilder.Draw += _windowSystem.Draw;
        _uiBuilder.OpenConfigUi += _mainWindow.OpenSettings;
    }

    public void Dispose()
    {
        _uiBuilder.OpenMainUi -= _mainWindow.Toggle;
        _uiBuilder.Draw -= _windowSystem.Draw;
        _uiBuilder.OpenConfigUi -= _mainWindow.OpenSettings;
    }
}