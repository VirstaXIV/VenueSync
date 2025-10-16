using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using OtterGui.Services;
using VenueSync.Events;
using VenueSync.Services;
using VenueSync.Ui.Widgets;

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
    private readonly GuestListWidget _guestListWidget;
    private readonly VenueWindowPosition _position;
    
    private readonly ReloadMods _reloadMods;
    private readonly DisableMods _disableMods;
    
    public VenueWindow(IDalamudPluginInterface pluginInterface, Configuration configuration, SyncFileService syncFileService, GuestListWidget guestListWidget,
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
        _guestListWidget = guestListWidget;
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
            
            // Discord and Carrd Links (centered under logo)
            bool hasDiscord = !string.IsNullOrEmpty(venue.discord_invite);
            bool hasCarrd = !string.IsNullOrEmpty(venue.carrd_url);
            
            if (hasDiscord || hasCarrd)
            {
                var buttonWidth = 105f;
                var spacing = 5f;
                var totalWidth = hasDiscord && hasCarrd ? (buttonWidth * 2 + spacing) : buttonWidth;
                var startX = (sidebarWidth - totalWidth) / 2;
                
                ImGui.SetCursorPosX(startX);
                
                if (hasDiscord)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, VenueColors.DiscordButton);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, VenueColors.DiscordButtonHover);
                    if (ImGui.Button("Discord", new Vector2(buttonWidth, 30)))
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
        
                if (hasDiscord && hasCarrd)
                {
                    ImGui.SameLine(0, spacing);
                }
        
                if (hasCarrd)
                {
                    if (!hasDiscord)
                    {
                        ImGui.SetCursorPosX(startX);
                    }
            
                    ImGui.PushStyleColor(ImGuiCol.Button, VenueColors.CarrdButton);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, VenueColors.CarrdButtonHover);
                    if (ImGui.Button("Carrd", new Vector2(buttonWidth, 30)))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = venue.carrd_url,
                            UseShellExecute = true
                        });
                    }
                    ImGui.PopStyleColor(2);
            
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(venue.carrd_url);
                }
            
                ImGui.Spacing();
            }
            
            var liveStream = venue.streams.FirstOrDefault();
            if (liveStream != null)
            {
                var streamButtonWidth = 220f;
                ImGui.SetCursorPosX((sidebarWidth - streamButtonWidth) / 2);
        
                ImGui.PushStyleColor(ImGuiCol.Button, VenueColors.TwitchButton);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, VenueColors.TwitchButtonHover);
        
                var buttonText = $"{liveStream.name} LIVE";
                if (ImGui.Button(buttonText, new Vector2(streamButtonWidth, 30)))
                {
                    if (liveStream.type == "twitch")
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = $"https://twitch.tv/{liveStream.name}",
                            UseShellExecute = true
                        });
                    }
                }
                ImGui.PopStyleColor(2);
        
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{liveStream.title}\n{liveStream.viewers} viewers");
            
                ImGui.Spacing();
            }
            
            if (venue.location.mods.Count > 0)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                ImGui.TextColored(VenueColors.SectionHeader, "Venue Mods");
                ImGui.Spacing();
                
                const float buttonSpacing = 5f;
                const float buttonHeight = 30f;
                const float downloadContainerHeight = 65f;
                const float downloadButtonHeight = 30f;
                
                var reservedHeight = buttonSpacing + (buttonHeight * 2) + buttonSpacing + 3f;
                
                if (_syncFileService.IsDownloading)
                {
                    reservedHeight += buttonSpacing + downloadContainerHeight + downloadButtonHeight + buttonSpacing + 2f;
                }
                
                var modsListHeight = ImGui.GetContentRegionAvail().Y - reservedHeight;
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
                        ImGui.TextColored(VenueColors.ActiveIndicator, "✓");
                    }
                    
                    ImGui.Spacing();
                    
                    ImGui.PopID();
                }
                
                ImGui.EndChild();

                if (_syncFileService.IsDownloading)
                {
                    ImGui.Spacing();
                    DrawDownloadProgress(
                        _syncFileService.OverallDownloadProgressString, 
                        _syncFileService.OverallDownloadProgress, 
                        _syncFileService.ActiveDownloadCount);
                }
                
                ImGui.Spacing();
                
                ImGui.PushStyleColor(ImGuiCol.Button, VenueColors.ReloadButton);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, VenueColors.ReloadButtonHover);
                if (ImGui.Button("Reload All Mods", new Vector2(-1, 30)))
                {
                    _reloadMods.Invoke();
                }
                ImGui.PopStyleColor(2);
                
                ImGui.PushStyleColor(ImGuiCol.Button, VenueColors.DisableButton);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, VenueColors.DisableButtonHover);
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
            ImGui.SetWindowFontScale(1.5f);
            ImGui.TextColored(VenueColors.VenueName, _stateService.VenueState.name);
            ImGui.SetWindowFontScale(1.0f);
            
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

                if (_stateService.VisitorsState.players.ContainsKey(_stateService.PlayerState.name))
                {
                    if (ImGui.BeginTabItem("Guests"))
                    {
                        DrawGuestsTab();
                        ImGui.EndTabItem();
                    }
                }

                /*if (ImGui.BeginTabItem("Events"))
                {
                    DrawEventsTab();
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
    }
    
    private void DrawOverviewTab()
    {
        var venue = _stateService.VenueState;
        
        var descWidth = ImGui.GetContentRegionAvail().X - 20;
        var wrappedTextSize = ImGui.CalcTextSize(venue.description, false, descWidth);
        var descHeight = wrappedTextSize.Y + 20;
    
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VenueColors.DescriptionBackground);
        ImGui.BeginChild("DescriptionBox", new Vector2(0, descHeight), true);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
        ImGui.TextColored(VenueColors.DescriptionText, venue.description);
        ImGui.PopTextWrapPos();
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.Spacing();
        
        ImGui.Text($"Location: ");
        ImGui.SameLine();
        ImGui.TextColored(VenueColors.LocationText, 
                          $"{venue.location.world} - {venue.location.district} - Ward {venue.location.ward}, Plot {venue.location.plot}");
        
        ImGui.Text($"Open Hours: ");
        ImGui.Spacing();
        ImGui.TextColored(VenueColors.OpenHours, venue.open_hours != string.Empty ? venue.open_hours : "N/A");

        ImGui.Spacing();
        
        ImGui.Text($"Tags: ");
        foreach (var tag in venue.tags)
        {
            ImGui.SameLine();
            ImGui.TextColored(VenueColors.Tag, $"[{tag}]");
        }
        
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (venue.streams.Count > 0)
        {
            ImGui.Spacing();
            ImGui.Text("Live Streams:");
            ImGui.Spacing();
            
            foreach (var stream in venue.streams)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, VenueColors.TwitchButton);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, VenueColors.TwitchButtonHover);
                
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
        
            ImGui.TextColored(VenueColors.StaffName, staff.name);
            ImGui.SameLine();
            ImGui.TextColored(VenueColors.StaffPosition, $"- {staff.position}");
        
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        
            ImGui.PopID();
        }
    
        ImGui.EndChild();
    }
    
    private void DrawGuestsTab()
    {
        var visitors = _stateService.VisitorsState;
    
        ImGui.Text("Guests");
        ImGui.Separator();
        ImGui.Spacing();

        if (visitors.players.Count == 0)
        {
            ImGui.TextWrapped("No guest information available.");
            return;
        }

        ImGui.BeginChild("GuestListFull", new Vector2(0, 0), true);
        
        _guestListWidget.Draw();
    
        ImGui.EndChild();
    }
    
    private void DrawDownloadProgress(string progressText, double currentProgress, int active)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, VenueColors.ProgressBackground);
        ImGui.BeginChild("DownloadProgress", new Vector2(0, 65), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
    
        int dotCount = ((int)(ImGui.GetTime() * 3) % 4);
        string dots = new string('.', dotCount);
    
        ImGui.Text($"Downloading{dots,-3}");
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(progressText).X + ImGui.GetCursorPosX());
        ImGui.TextColored(VenueColors.ProgressText, progressText);

        float progress = (float)(currentProgress / 100.0);
    
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.15f, 0.15f, 0.2f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, VenueColors.ProgressBar);
        ImGui.ProgressBar(progress, new Vector2(-1, 8), "");
        ImGui.PopStyleColor(2);

        ImGui.Text($"{active} active file(s)");

        ImGui.EndChild();
        ImGui.PopStyleColor();
    
        ImGui.PushStyleColor(ImGuiCol.Button, VenueColors.DisableButton);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, VenueColors.DisableButtonHover);
        if (ImGui.Button("Cancel Downloads", new Vector2(-1, 30)))
        {
            _syncFileService.CancelAllDownloads();
        }
        ImGui.PopStyleColor(2);
    }
}
