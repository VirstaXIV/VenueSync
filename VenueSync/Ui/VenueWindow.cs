using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Utility;
using OtterGui.Services;
using VenueSync.Data;
using VenueSync.Events;
using VenueSync.Services;
using VenueSync.Services.Api;
using VenueSync.State;
using VenueSync.Ui.Widgets;

namespace VenueSync.Ui;

public class VenueWindowPosition : IService
{
    public bool IsOpen { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size { get; set; }
}

public class VenueWindow : Window, IDisposable
{
    private const float SidebarWidth = 280f;
    private const float LogoSize = 250f;
    private const float ButtonWidth = 105f;
    private const float ButtonSpacing = 5f;
    private const float ButtonHeight = 30f;
    private const float StreamButtonWidth = 220f;

    private readonly Configuration _configuration;
    private readonly VenueSettings _venueSettings;
    private readonly StateService _stateService;
    private readonly LocationApi _locationApi;
    private readonly SyncFileService _syncFileService;
    private readonly GuestListWidget _guestListWidget;
    private readonly StaffListWidget _staffListWidget;
    private readonly VenueWindowPosition _position;
    private readonly ReloadMods _reloadMods;
    private readonly DisableMods _disableMods;
    private bool _pendingModChanges;

    public VenueWindow(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        VenueSettings venueSettings,
        SyncFileService syncFileService,
        GuestListWidget guestListWidget,
        StaffListWidget staffListWidget,
        StateService stateService,
        LocationApi locationApi,
        VenueWindowPosition position,
        ReloadMods reloadMods,
        DisableMods disableMods,
        ServiceDisconnected serviceDisconnected) : base("VenueSyncVenueWindow")
    {
        pluginInterface.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 700),
            MaximumSize = new Vector2(1000, 900),
        };
        
        _configuration = configuration;
        _venueSettings = venueSettings;
        _stateService = stateService;
        _locationApi = locationApi;
        _syncFileService = syncFileService;
        _guestListWidget = guestListWidget;
        _staffListWidget = staffListWidget;
        _position = position;
        _reloadMods = reloadMods;
        _disableMods = disableMods;

        serviceDisconnected.Subscribe(OnDisconnect, ServiceDisconnected.Priority.High);
    }

    private void OnDisconnect()
    {
        if (IsOpen)
        {
            Toggle();
        }
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
        DrawSidebar();
        ImGui.SameLine();
        DrawMainContent();
    }

    private void DrawSidebar()
    {
        var venue = _stateService.VenueState;

        ImGui.BeginChild("LeftSidebar", new Vector2(SidebarWidth, 0), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        
        DrawLogo(venue);
        DrawExternalLinks(venue);
        DrawLiveStream(venue);
        DrawVenueMods(venue);
        
        ImGui.EndChild();
    }

    private void DrawLogo(VenueState venue)
    {
        if (venue.logoTexture == null) return;

        var cursorPos = ImGui.GetCursorPos();
        ImGui.SetCursorPosX(cursorPos.X + (SidebarWidth - LogoSize) / 2);
        ImGui.Image(venue.logoTexture.Handle, new Vector2(LogoSize, LogoSize));
        ImGui.Spacing();
    }

    private void DrawExternalLinks(VenueState venue)
    {
        bool hasDiscord = !string.IsNullOrEmpty(venue.discord_invite);
        bool hasCarrd = !string.IsNullOrEmpty(venue.carrd_url);

        if (!hasDiscord && !hasCarrd) return;

        var totalWidth = hasDiscord && hasCarrd ? (ButtonWidth * 2 + ButtonSpacing) : ButtonWidth;
        var startX = (SidebarWidth - totalWidth) / 2;

        ImGui.SetCursorPosX(startX);

        if (hasDiscord)
        {
            DrawStyledButton("Discord", VenueColors.DiscordButton, VenueColors.DiscordButtonHover, 
                new Vector2(ButtonWidth, ButtonHeight), () => Util.OpenLink(venue.discord_invite), venue.discord_invite);

            if (hasCarrd)
            {
                ImGui.SameLine(0, ButtonSpacing);
            }
        }

        if (hasCarrd)
        {
            if (!hasDiscord)
            {
                ImGui.SetCursorPosX(startX);
            }

            DrawStyledButton("Carrd", VenueColors.CarrdButton, VenueColors.CarrdButtonHover, 
                new Vector2(ButtonWidth, ButtonHeight), () => Util.OpenLink(venue.carrd_url), venue.carrd_url);
        }

        ImGui.Spacing();
    }

    private static string GetPlatformDisplayName(string? type)
    {
        return (type?.ToLowerInvariant()) switch
        {
            "kick" => "Kick",
            "youtube" => "YouTube",
            _ => "Twitch",
        };
    }

    private static string GetStreamUrl(VenueStream stream)
    {
        var t = stream.type.ToLowerInvariant();
        return t switch
        {
            "kick" => $"https://kick.com/{stream.username}",
            "youtube" => $"https://youtube.com/@{stream.username}",
            _ => $"https://twitch.tv/{stream.username}",
        };
    }

    private void DrawLiveStream(VenueState venue)
    {
        var liveStream = venue.streams.FirstOrDefault(v => v.name == venue.active_stream);
        if (liveStream == null) return;

        float streamLogoSize = LogoSize / 4f;
        var platform = GetPlatformDisplayName(liveStream.type);
        var url = GetStreamUrl(liveStream);
        var tooltip = $"Watch {liveStream.name} live on {platform}";

        if (liveStream.logoTexture != null)
        {
            ImGui.SetCursorPosX((SidebarWidth - streamLogoSize) / 2);
            ImGui.Image(liveStream.logoTexture.Handle, new Vector2(streamLogoSize, streamLogoSize));

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                Util.OpenLink(url);
            }
        }
        else
        {
            ImGui.SetCursorPosX((SidebarWidth - StreamButtonWidth) / 2);

            var buttonText = $"{liveStream.name}";
            DrawStyledButton(buttonText, VenueColors.TwitchButton, VenueColors.TwitchButtonHover, 
                new Vector2(StreamButtonWidth, ButtonHeight), 
                () => Util.OpenLink(url), 
                tooltip);
        }

        ImGui.Spacing();
    }

    private void DrawVenueMods(VenueState venue)
    {
        if (venue.location.mods.Count == 0) return;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(VenueColors.SectionHeader, "Venue Mods");
        ImGui.Spacing();

        DrawModsList(venue);
        DrawDownloadProgressIfActive();
        DrawModActionButtons();
        ApplyPendingModChangesIfAny();
    }

    private void DrawModsList(VenueState venue)
    {
        const float downloadContainerHeight = 65f;
        const float downloadButtonHeight = 30f;

        var reservedHeight = ButtonSpacing + (ButtonHeight * 2) + ButtonSpacing + 3f;

        if (_syncFileService.IsDownloading)
        {
            reservedHeight += ButtonSpacing + downloadContainerHeight + downloadButtonHeight + ButtonSpacing + 2f;
        }

        var modsListHeight = ImGui.GetContentRegionAvail().Y - reservedHeight;
        ImGui.BeginChild("ModsList", new Vector2(0, modsListHeight), true);

        for (int i = 0; i < venue.location.mods.Count; i++)
        {
            DrawModCheckbox(venue.location.mods[i], i);
        }

        ImGui.EndChild();
    }

    private void DrawModCheckbox(MannequinModItem mod, int index)
    {
        var id = mod.id;
        var isEnabledFromSettings = _configuration.AutoloadMods
            ? !_venueSettings.InactiveMods.Contains(id)
            : _venueSettings.ActiveMods.Contains(id);

        var failed = _stateService.VenueState.failed_mods.Contains(id);

        var displayEnabled = isEnabledFromSettings && !failed;

        var label = $"{mod.name}##{index}";
        if (ImGui.Checkbox(label, ref displayEnabled))
        {
            var userEnabled = displayEnabled;

            var changed = UpdateModState(id, userEnabled);

            if (userEnabled && failed)
            {
                _stateService.VenueState.failed_mods.Remove(id);
            }

            if (changed)
            {
                _pendingModChanges = true;
            }
        }

        if (failed)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.85f, 0.2f, 0.2f, 1f), " (Failed)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("This mod failed to apply previously. Enable to retry.");
            }
        }
    }

    private bool UpdateModState(string id, bool isEnabled)
    {
        bool changed = false;
        if (_configuration.AutoloadMods)
        {
            if (isEnabled)
            {
                var removed = _venueSettings.InactiveMods.RemoveAll(m => m == id);
                if (removed > 0) changed = true;
            }
            else
            {
                if (!_venueSettings.InactiveMods.Contains(id))
                {
                    _venueSettings.InactiveMods.Add(id);
                    changed = true;
                }
            }
        }
        else
        {
            if (isEnabled)
            {
                if (!_venueSettings.ActiveMods.Contains(id))
                {
                    _venueSettings.ActiveMods.Add(id);
                    changed = true;
                }
            }
            else
            {
                var removed = _venueSettings.ActiveMods.RemoveAll(m => m == id);
                if (removed > 0) changed = true;
            }
        }

        return changed;
    }

    private void DrawDownloadProgressIfActive()
    {
        if (!_syncFileService.IsDownloading) return;

        ImGui.Spacing();
        DrawDownloadProgress(
            _syncFileService.OverallDownloadProgressString,
            _syncFileService.OverallDownloadProgress,
            _syncFileService.ActiveDownloadCount);
    }

    private void DrawModActionButtons()
    {
        ImGui.Spacing();

        DrawStyledButton("Reload All Mods", VenueColors.ReloadButton, VenueColors.ReloadButtonHover, 
            new Vector2(-1, ButtonHeight), () => _reloadMods.Invoke(), null);

        DrawStyledButton("Disable All Mods", VenueColors.DisableButton, VenueColors.DisableButtonHover, 
            new Vector2(-1, ButtonHeight), () => _disableMods.Invoke(), "Temporarily disable all venue mods");
    }

    private void ApplyPendingModChangesIfAny()
    {
        if (!_pendingModChanges)
            return;

        _venueSettings.Save();
        _reloadMods.Invoke();
        _pendingModChanges = false;
    }

    private void DrawMainContent()
    {
        var venue = _stateService.VenueState;

        ImGui.BeginChild("RightContent", new Vector2(0, 0));

        ImGui.SetWindowFontScale(1.5f);
        ImGui.TextColored(VenueColors.VenueName, venue.name);
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

            if (IsCurrentPlayerStaff())
            {
                if (ImGui.BeginTabItem("Moderation"))
                {
                    DrawModerationTab();
                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }

        ImGui.EndChild();
    }

    private void DrawOverviewTab()
    {
        var venue = _stateService.VenueState;

        DrawDescription(venue.description);
        DrawLocationInfo(venue);
        DrawOpenHours(venue.open_hours);
        DrawTags(venue.tags);
        DrawStreamsList(venue.streams);
    }

    private void DrawDescription(string description)
    {
        var descWidth = ImGui.GetContentRegionAvail().X - 20;
        var wrappedTextSize = ImGui.CalcTextSize(description, false, descWidth);
        var descHeight = wrappedTextSize.Y + 20;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, VenueColors.DescriptionBackground);
        ImGui.BeginChild("DescriptionBox", new Vector2(0, descHeight), true);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
        ImGui.TextColored(VenueColors.DescriptionText, description);
        ImGui.PopTextWrapPos();
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void DrawLocationInfo(VenueState venue)
    {
        ImGui.Text("Location: ");
        ImGui.SameLine();
        ImGui.TextColored(VenueColors.LocationText,
            $"{venue.location.world} - {venue.location.district} - Ward {venue.location.ward}, Plot {venue.location.plot}");
    }

    private void DrawOpenHours(string openHours)
    {
        ImGui.Text("Open Hours: ");
        ImGui.Spacing();
        ImGui.TextColored(VenueColors.OpenHours, !string.IsNullOrEmpty(openHours) ? openHours : "N/A");
        ImGui.Spacing();
    }

    private void DrawTags(List<string> tags)
    {
        ImGui.Text("Tags: ");
        foreach (var tag in tags)
        {
            ImGui.SameLine();
            ImGui.TextColored(VenueColors.Tag, $"[{tag}]");
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawStreamsList(List<VenueStream> streams)
    {
        if (streams.Count == 0) return;

        ImGui.Spacing();
        ImGui.TextColored(VenueColors.SectionHeader, "Streams");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var i = 1;
        foreach (var stream in streams)
        {
            var isActive = stream.name == _stateService.VenueState.active_stream;
            var buttonText = stream.name;
            var platform = GetPlatformDisplayName(stream.type);
            var url = GetStreamUrl(stream);
            var tooltip = $"Visit {stream.name} on {platform}";

            float iconSize = LogoSize / 4f;

            if (stream.logoTexture != null)
            {
                ImGui.Image(stream.logoTexture.Handle, new Vector2(iconSize, iconSize));

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(tooltip);
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    Util.OpenLink(url);
                }
            }
            else
            {
                var buttonColor = isActive ? VenueColors.TwitchButton : new Vector4(0.4f, 0.4f, 0.5f, 1.0f);
                var hoverColor = isActive ? VenueColors.TwitchButtonHover : new Vector4(0.5f, 0.5f, 0.6f, 1.0f);

                DrawStyledButton(buttonText, buttonColor, hoverColor,
                    new Vector2(100, 30), () => Util.OpenLink(url), tooltip);
            }

            if (i % 4 != 0)
            {
                ImGui.SameLine();
            }
            else
            {
                ImGui.Spacing();
            }
            i++;
        }
    }

    private void DrawStaffTab()
    {
        var venue = _stateService.VenueState;

        if (venue.staff.Count == 0)
        {
            ImGui.TextWrapped("No staff information available.");
            return;
        }

        ImGui.BeginChild("StaffListFull", new Vector2(0, 0), true);
        _staffListWidget.Draw();
        ImGui.EndChild();
    }

    private void DrawGuestsTab()
    {
        var visitors = _stateService.VisitorsState;

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

        DrawStyledButton("Cancel Downloads", VenueColors.DisableButton, VenueColors.DisableButtonHover,
            new Vector2(-1, ButtonHeight), () => _syncFileService.CancelAllDownloads(), null);
    }

    private void DrawStyledButton(string text, Vector4 buttonColor, Vector4 hoverColor, Vector2 size, Action onClick, string? tooltip)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hoverColor);
        
        if (ImGui.Button(text, size))
        {
            onClick();
        }
        
        ImGui.PopStyleColor(2);

        if (tooltip != null && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }
    }

    private bool IsCurrentPlayerStaff()
    {
        var venue = _stateService.VenueState;
        var playerName = _stateService.PlayerState.name;

        return venue.staff.Any(staff => staff.name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
    }

    private void DrawModerationTab()
    {
        var venue = _stateService.VenueState;

        ImGui.Spacing();
        ImGui.TextColored(VenueColors.SectionHeader, "Active Stream");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (venue.streams.Count == 0)
        {
            ImGui.TextWrapped("No streams available.");
            return;
        }

        ImGui.Text("Select Stream:");
        ImGui.Spacing();

        var streamNames = venue.streams.Select(s => $"{s.name} ({s.type})").ToArray();
        var currentIndex = string.IsNullOrEmpty(venue.active_stream) 
            ? 0 
            : venue.streams.FindIndex(s => s.name == venue.active_stream);
        
        if (currentIndex < 0) currentIndex = 0;

        if (ImGui.Combo("##StreamSelect", ref currentIndex, streamNames, streamNames.Length))
        {
            var selectedStream = venue.streams[currentIndex];
            OnStreamSelected(selectedStream.name);
        }
        if (ImGui.Button("No Stream Active"))
        {
            OnStreamSelected(string.Empty);
        }
    }

    private void OnStreamSelected(string streamId)
    {
        VenueSync.Log.Information($"Stream selected: {streamId}");

        _ = Task.Run(async () => await _locationApi.SendActiveStream(_stateService.VenueState.id, _stateService.VenueState.location.id, streamId).ConfigureAwait(false));
    }
}
