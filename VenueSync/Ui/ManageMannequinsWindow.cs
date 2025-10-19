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
using System.Collections.Generic;

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
    
    private readonly HashSet<string> _updatingMannequins = new();

    public ManageMannequinsWindow(
        IDalamudPluginInterface pluginInterface,
        StateService            stateService,
        ManageMannequinsWindowPosition position,
        ActorObjectManager      objects,
        MannequinService        mannequinService
    ) : base("VenueSyncManageMannequinsWindow")
    {
        pluginInterface.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new Window.WindowSizeConstraints()
        {
            MinimumSize = new Vector2(400, 350),
            MaximumSize = new Vector2(400, 350),
        };
        _stateService     = stateService;
        _mannequinService = mannequinService;
        _objects          = objects;
        _position         = position;
    }

    public override void PreDraw()
    {
        _position.IsOpen = IsOpen;
        WindowName       = "Manage Mannequins###VenueSyncManageMannequinsWindow";
    }

    public void Dispose()
    {
        _position.IsOpen = false;
    }

    public override void Draw()
    {
        _position.Size     = ImGui.GetWindowSize();
        _position.Position = ImGui.GetWindowPos();

        if (!_stateService.Connection.Connected)
        {
            ImGui.TextUnformatted("Not Connected");
            return;
        }

        if (_stateService.CurrentHouse.HouseId == 0)
        {
            ImGui.TextUnformatted("Enter your house to manage mannequins.");
            return;
        }

        ImGui.TextUnformatted("Manage Mannequins");

        ImGui.Spacing();
        ImGui.PushTextWrapPos(0f);
        ImGui.TextUnformatted(
            "This list shows all mannequins detected in your current house. " +
            "Click Update to send each mannequin to the service and refresh related data."
        );
        ImGui.PopTextWrapPos();
        ImGui.Spacing();

        var mannequins = _objects
            .Where(p => p.Value.Objects.Any(a => a.Model))
            .Where(p => p.Key.Type is IdentifierType.Retainer)
            .ToList();

        if (mannequins.Count == 0)
        {
            ImGui.TextUnformatted("No mannequins were detected in this house.");
            return;
        }

        if (ImGui.BeginTable("MannequinsTable", 2))
        {
            ImGui.TableSetupColumn("Mannequin");
            ImGui.TableSetupColumn("Action");
            ImGui.TableHeadersRow();

            foreach (var mannequin in mannequins)
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(mannequin.Value.Label);

                ImGui.TableNextColumn();
                var label = mannequin.Value.Label;
                var isUpdating = _updatingMannequins.Contains(label);

                ImGui.BeginDisabled(isUpdating);
                if (ImGui.Button($"Update##{label}"))
                {
                    _updatingMannequins.Add(label);
                    _ = UpdateMannequinAsync(label);
                    VenueSync.Log.Debug($"Mannequin update requested for {label}");
                }
                ImGui.EndDisabled();

                if (!isUpdating)
                {
                    continue;
                }
                ImGui.SameLine();
                ImGui.TextUnformatted("Updating...");
            }

            ImGui.EndTable();
        }
    }

    private async Task UpdateMannequinAsync(string label)
    {
        try
        {
            var mannequinData = new Mannequin
            {
                ffxiv_id    = _stateService.CurrentHouse!.HouseId,
                name        = label,
                world       = _stateService.PlayerState.world,
                data_center = _stateService.PlayerState.data_center,
            };

            var reply = await _mannequinService.UpdateMannequin(mannequinData);
            if (reply.Success)
            {
                VenueSync.Log.Debug("Mannequin updated successfully.");
            }
            else
            {
                VenueSync.Log.Warning($"Mannequin update failed: {reply.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Log.Warning($"Unexpected error while updating mannequin '{label}': {ex.Message}");
        }
        finally
        {
            _updatingMannequins.Remove(label);
        }
    }
}
