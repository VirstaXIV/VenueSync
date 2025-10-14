using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Services;
using OtterGui.Text;
using VenueSync.Events;
using VenueSync.Services;

namespace VenueSync.Ui;

public class VenueWindowPosition : IService
{
    public bool    IsOpen   { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size     { get; set; }
}

public class VenueWindow : Window, IDisposable
{
    private readonly Configuration _configuration;
    private readonly StateService _stateService;
    private readonly SyncFileService _syncFileService;
    private readonly VenueWindowPosition _position;
    
    private readonly ReloadMods _reloadMods;
    private readonly DisableMods _disableMods;
    
    public VenueWindow(IDalamudPluginInterface pluginInterface, Configuration configuration, SyncFileService syncFileService,
        StateService stateService, VenueWindowPosition position, ReloadMods @reloadMods, DisableMods @disableMods) : base("VenueSyncVenueWindow")
    {
        pluginInterface.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(800, 600),
            MaximumSize = new Vector2(800, 600),
        };
        _configuration = configuration;
        _stateService = stateService;
        _syncFileService = syncFileService;
        _position = position;
        
        _reloadMods = @reloadMods;
        _disableMods = @disableMods;
    }
    
    public override void PreDraw()
    {
        _position.IsOpen = IsOpen;
        WindowName = $"Venue: {_stateService.VenueState.name}###VenueSyncVenueWindow";
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
        
        ImGui.TextUnformatted("Inside Venue! :D");

        foreach (var mod in _stateService.VenueState.location.mods)
        {
            Checkbox(mod.name, $"Enable {mod.name}",
                     _configuration.ActiveMods.Contains(mod.mannequin_id), v =>
                     {
                         if (_configuration.ActiveMods.Contains(mod.mannequin_id))
                         {
                             _configuration.ActiveMods.Remove(mod.mannequin_id);
                         }
                         else
                         {
                             _configuration.ActiveMods.Add(mod.mannequin_id);
                         }
                         _reloadMods.Invoke();
                     });
        }

        if (_stateService.VenueState.logoTexture != null)
        {
            ImGui.Image(_stateService.VenueState.logoTexture.Handle, new Vector2(250, 250));
        }

        ImGui.BeginDisabled(_syncFileService.IsDownloading);
        if (ImGui.Button("Reload Mods"))
        {
            _reloadMods.Invoke();
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable Mods"))
        {
            _disableMods.Invoke();
        }
        ImGui.EndDisabled();

        DrawDownloadProgress();
    }
    
    public void DrawDownloadProgress()
    {
        if (!_syncFileService.IsDownloading)
        {
            return;
        }

        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.25f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.3f, 0.7f, 0.3f, 1.0f));
    
        float progress = (float)(_syncFileService.OverallDownloadProgress / 100.0);
    
        // Animated loading text
        string[] spinner = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        int spinnerIndex = (int)(ImGui.GetTime() * 10) % spinner.Length;
    
        ImGui.Text($"{spinner[spinnerIndex]} Downloading Mod Files");
        ImGui.ProgressBar(progress, new Vector2(-1, 25), "");
    
        // Overlay text on progress bar
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() - ImGui.CalcTextSize(_syncFileService.OverallDownloadProgressString).X / 2 - ImGui.GetContentRegionAvail().X / 2);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 21);
        ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), _syncFileService.OverallDownloadProgressString);
    
        ImGui.Text($"Files: {_syncFileService.ActiveDownloadCount} active");
    
        ImGui.PopStyleColor(2);
    
        if (ImGui.Button("Cancel All##downloads", new Vector2(-1, 0)))
        {
            _syncFileService.CancelAllDownloads();
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(string label, string tooltip, bool current, Action<bool> setter)
    {
        var tmp = current;
        if (ImUtf8.Checkbox(""u8, ref tmp) && tmp != current)
        {
            setter(tmp);
            _configuration.Save();
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker(label, tooltip);
    }
}
