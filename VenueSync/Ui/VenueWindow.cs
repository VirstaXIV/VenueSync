using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Services;
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
            MinimumSize = new Vector2(800, 700),
            MaximumSize = new Vector2(1000, 900),
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
        var venue = _stateService.VenueState;
        var sidebarWidth = 280f;
        
        ImGui.BeginChild("LeftSidebar", new Vector2(sidebarWidth, 0), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        {
            if (venue.logoTexture != null)
            {
                var cursorPos = ImGui.GetCursorPos();
                ImGui.SetCursorPosX(cursorPos.X + (sidebarWidth - 250) / 2);
                ImGui.Image(venue.logoTexture.Handle, new Vector2(250, 250));
                ImGui.Spacing();
            }
            
            if (venue.location.mods.Count > 0)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                ImGui.TextColored(new Vector4(1f, 0.7f, 0.4f, 1f), "Venue Mods");
                ImGui.Spacing();
                
                var modsListHeight = ImGui.GetContentRegionAvail().Y - 75; // Reserve space for buttons
                ImGui.BeginChild("ModsList", new Vector2(0, modsListHeight), true);
                
                foreach (var mod in venue.location.mods)
                {
                    ImGui.PushID(mod.id);
                    
                    bool isEnabled = _configuration.ActiveMods.Contains(mod.mannequin_id);
                    if (ImGui.Checkbox($"##modToggle{mod.id}", ref isEnabled))
                    {
                        if (_configuration.ActiveMods.Contains(mod.mannequin_id) != isEnabled)
                        {
                            if (isEnabled)
                            {
                                _configuration.ActiveMods.Add(mod.mannequin_id);
                            }
                            else
                            {
                                _configuration.ActiveMods.Remove(mod.mannequin_id);
                            }

                            _reloadMods.Invoke();
                        }
                    }
                    
                    ImGui.SameLine();
                    ImGui.TextWrapped(mod.name);
                    
                    if (!string.IsNullOrEmpty(mod.description))
                    {
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip(mod.description);
                    }
                    
                    if (_configuration.ActiveMods.Contains(mod.mannequin_id))
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), "✓");
                    }
                    
                    ImGui.Spacing();
                    
                    ImGui.PopID();
                }
                
                ImGui.EndChild();
                
                ImGui.Spacing();
                
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.8f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.9f, 1f));
                if (ImGui.Button("Reload All Mods", new Vector2(-1, 30)))
                {
                    _reloadMods.Invoke();
                }
                ImGui.PopStyleColor(2);
                
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.3f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.4f, 0.4f, 1f));
                if (ImGui.Button("Disable All Mods", new Vector2(-1, 30)))
                {
                    _disableMods.Invoke();
                }
                ImGui.PopStyleColor(2);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Temporarily disable all venue mods");
            }
        }
        ImGui.EndChild();
        ImGui.SameLine();
        
        ImGui.BeginChild("RightContent", new Vector2(0, 0), false);
        {
            if (ImGui.BeginTabBar("VenueTabs"))
            {
                if (ImGui.BeginTabItem("Overview"))
                {
                    DrawOverviewTab();
                    ImGui.EndTabItem();
                }
                
                if (venue.staff.Count > 0 && ImGui.BeginTabItem("Staff"))
                {
                    DrawStaffTab();
                    ImGui.EndTabItem();
                }

                /*if (ImGui.BeginTabItem("Events"))
                {
                    DrawEventsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Staff"))
                {
                    DrawStaffTab();
                    ImGui.EndTabItem();
                }

                // Conditional moderation tab
                if (isModerator && ImGui.BeginTabItem("Moderation"))
                {
                    DrawModerationTab();
                    ImGui.EndTabItem();
                }*/

                ImGui.EndTabBar();
            }
        }
        ImGui.EndChild();
        
        DrawDownloadProgress();
    }
    
    private void DrawOverviewTab()
    {
        var venue = _stateService.VenueState;
        
        var contentWidth = ImGui.GetContentRegionAvail().X;
        var textSize = ImGui.CalcTextSize(_stateService.VenueState.name);
        ImGui.SetCursorPosX((contentWidth - textSize.X) / 2);
        ImGui.TextColored(new Vector4(1f, 0.8f, 0.4f, 1f), _stateService.VenueState.name);
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        ImGui.Text("Description:");
        ImGui.Spacing();
        
        ImGui.Indent(10);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1f), venue.description);
        ImGui.Unindent(10);

        ImGui.Spacing();
        ImGui.Spacing();
        
        ImGui.Text($"Location: ");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), 
            $"{venue.location.world} - {venue.location.district} - Ward {venue.location.ward}, Plot {venue.location.plot}");
        
        ImGui.Text($"Open Hours: ");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.6f, 1f, 0.6f, 1f), venue.open_hours);
        
        ImGui.Spacing();
        
        ImGui.Text($"Tags: ");
        foreach (var tag in venue.tags)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.9f, 0.7f, 1f, 1f), $"[{tag}]");
        }
        
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Links");
        ImGui.Spacing();

        if (!string.IsNullOrEmpty(venue.discord_invite))
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.35f, 0.4f, 0.9f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.45f, 0.5f, 1f, 1f));
            if (ImGui.Button($"Join Discord Server", new Vector2(200, 30)))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = venue.discord_invite,
                    UseShellExecute = true
                });
            }
            ImGui.PopStyleColor(2);
            
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(venue.discord_invite);
        }

        if (venue.streams.Count > 0)
        {
            ImGui.Spacing();
            ImGui.Text("Live Streams:");
            ImGui.Spacing();
            
            foreach (var stream in venue.streams)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.58f, 0.29f, 0.78f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.68f, 0.39f, 0.88f, 1f));
                
                var buttonText = stream.live 
                    ? $"{stream.name} - LIVE ({stream.viewers} viewers)" 
                    : $"{stream.name}";
                
                if (ImGui.Button(buttonText, new Vector2(400, 25)))
                {
                    if (stream.type == "twitch")
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                            FileName = $"https://twitch.tv/{stream.name}",
                            UseShellExecute = true
                        });
                    }
                }
                ImGui.PopStyleColor(2);
                
                if (ImGui.IsItemHovered() && stream.live)
                    ImGui.SetTooltip($"Streaming: {stream.title}");
            }
        }
    }
    
    private void DrawStaffTab()
    {
        var venue = _stateService.VenueState;
    
        ImGui.Text("Venue Staff");
        ImGui.Separator();
        ImGui.Spacing();

        if (venue.staff.Count == 0)
        {
            ImGui.TextWrapped("No staff information available.");
            return;
        }

        ImGui.BeginChild("StaffListFull", new Vector2(0, 0), true);
    
        foreach (var staff in venue.staff)
        {
            ImGui.PushID(staff.name);
        
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.4f, 1f), staff.name);
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"- {staff.position}");
        
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        
            ImGui.PopID();
        }
    
        ImGui.EndChild();
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
}
