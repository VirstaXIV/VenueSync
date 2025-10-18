using System;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace VenueSync.Ui;

public class VenueSyncWindowSystem: IDisposable
{
    private readonly WindowSystem _windowSystem = new("VenueSync");
    private readonly IUiBuilder _uiBuilder;
    private readonly MainWindow _mainWindow;
    private readonly VenueWindow _venueWindow;
    
    public VenueSyncWindowSystem(IUiBuilder uiBuilder, MainWindow mainWindow, VenueWindow venueWindow, HouseVerifyWindow houseVerifyWindow, ManageMannequinsWindow mannequinsWindow)
    {
        _uiBuilder = uiBuilder;
        _mainWindow = mainWindow;
        _venueWindow = venueWindow;
        _windowSystem.AddWindow(mainWindow);
        _windowSystem.AddWindow(venueWindow);
        _windowSystem.AddWindow(houseVerifyWindow);
        _windowSystem.AddWindow(mannequinsWindow);
        
        _uiBuilder.OpenMainUi += _mainWindow.Toggle;
        _uiBuilder.Draw += _windowSystem.Draw;
        _uiBuilder.OpenConfigUi += _mainWindow.OpenSettings;
    }
    
    public bool VenueWindowOpened()
    {
        return _venueWindow.IsOpen;
    }

    public void ToggleVenueWindow()
    {
        _venueWindow.Toggle();
    }
    
    public void ToggleMainWindow()
    {
        _mainWindow.Toggle();
    }

    public void Dispose()
    {
        _uiBuilder.OpenMainUi -= _mainWindow.Toggle;
        _uiBuilder.Draw -= _windowSystem.Draw;
        _uiBuilder.OpenConfigUi -= _mainWindow.OpenSettings;
    }
}