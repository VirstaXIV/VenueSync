using System;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using VenueSync.Ui.Crud;
using VenueSync.Ui.Crud.Venue;

namespace VenueSync.Ui;

public class VenueSyncWindowSystem: IDisposable
{
    private readonly WindowSystem _windowSystem = new("VenueSync");
    private readonly IUiBuilder _uiBuilder;
    private readonly MainWindow _mainWindow;
    private readonly VenueWindow _venueWindow;
    
    public VenueSyncWindowSystem(
        IUiBuilder uiBuilder,
        MainWindow mainWindow,
        VenueWindow venueWindow,
        HouseVerifyWindow houseVerifyWindow,
        ManageMannequinsWindow mannequinsWindow,
        ManageVenueWindow manageVenueWindow,
        ManageStaffWindow manageStaffWindow,
        ManageHouseWindow manageHouseWindow,
        ManageStreamWindow manageStreamWindow,
        ManageScheduleWindow manageScheduleWindow,
        ManageLocationWindow manageLocationWindow,
        ModManagerWindow modManagerWindow)
    {
        _uiBuilder = uiBuilder;
        _mainWindow = mainWindow;
        _venueWindow = venueWindow;

        _windowSystem.AddWindow(mainWindow);
        _windowSystem.AddWindow(venueWindow);
        _windowSystem.AddWindow(houseVerifyWindow);
        _windowSystem.AddWindow(mannequinsWindow);
        _windowSystem.AddWindow(manageVenueWindow);
        _windowSystem.AddWindow(manageStaffWindow);
        _windowSystem.AddWindow(manageStreamWindow);
        _windowSystem.AddWindow(manageScheduleWindow);
        _windowSystem.AddWindow(manageLocationWindow);
        _windowSystem.AddWindow(manageHouseWindow);
        _windowSystem.AddWindow(modManagerWindow);
        
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