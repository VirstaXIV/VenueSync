using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Services;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using VenueSync.Services;
using VenueSync.State;

namespace VenueSync.Ui;

public class ManageMannequinsWindowPosition : IService
{
    public bool    IsOpen   { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size     { get; set; }
}

public class ManageMannequinsWindow : Window, IDisposable
{
    private readonly StateService _stateService;
    private readonly MannequinService _mannequinService;
    private readonly ManageMannequinsWindowPosition _position;
    private readonly ActorObjectManager _objects;
    
    private bool _isUpdating = false;
    
    public ManageMannequinsWindow(IDalamudPluginInterface pluginInterface,
        StateService stateService, ManageMannequinsWindowPosition position, ActorObjectManager objects,
        MannequinService mannequinService) : base("VenueSyncManageMannequinsWindow")
    {
        pluginInterface.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new Window.WindowSizeConstraints() {
            MinimumSize = new Vector2(400, 350),
            MaximumSize = new Vector2(400, 350),
        };
        _stateService = stateService;
        _mannequinService = mannequinService;
        _objects = objects;
        _position = position;
    }
    
    public override void PreDraw()
    {
        _position.IsOpen = IsOpen;
        WindowName = $"Manage Mannequins###VenueSyncManageMannequinsWindow";
    }

    public void Dispose()
    {
        _position.IsOpen = false;
    }

    public override void Draw()
    {
        _position.Size = ImGui.GetWindowSize();
        _position.Position = ImGui.GetWindowPos();
        
        if (!_stateService.Connection.Connected)
        {
            ImGui.TextUnformatted("Not Connected");
            return;
        }
        
        ImGui.TextUnformatted("Manage Mannequins");
        
        if (ImGui.BeginTable("Houses", 2))
        {
            var mannequinList = _objects
                .Where(p => p.Value.Objects.Any(a => a.Model))
                .Where(p => p.Key.Type is IdentifierType.Retainer);
            foreach (var mannequin in mannequinList)
            {
                ImGui.TableNextColumn();
                ImGui.TextColored(new Vector4(1,1,1,1), mannequin.Value.Label);
                ImGui.TableNextColumn();
                ImGui.BeginDisabled(_isUpdating);
                if (ImGui.Button("Update"))
                {
                    _isUpdating = true;
                    var mannequinToUpdate = mannequin.Value;
                    _ = Task.Run(async () =>
                    {
                        var mannequinData = new Mannequin() {
                            ffxiv_id = _stateService.CurrentHouse.HouseId,
                            name = mannequinToUpdate.Label,
                            world = _stateService.PlayerState.world,
                            data_center = _stateService.PlayerState.data_center,
                        };
                       
                        var reply = await _mannequinService.UpdateMannequin(mannequinData);
                        if (reply.Success)
                        {
                            VenueSync.Log.Debug("Mannequin Updated Successfully");
                        }
                        else
                        {
                            VenueSync.Log.Debug($"Mannequin Updated Failed: {reply.ErrorMessage}");
                        }
                        
                        _isUpdating = false;
                    });
                    VenueSync.Log.Debug("Mannequin Update requested");
                }
                ImGui.EndDisabled();
            }
            
            ImGui.EndTable();
        }
        
    }
}
