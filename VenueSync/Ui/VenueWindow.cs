using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Utility;
using OtterGui.Services;
using VenueSync.Data;
using VenueSync.Events;
using VenueSync.Services;
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
    private readonly SyncFileService _syncFileService;
    private readonly GuestListWidget _guestListWidget;
    private readonly StaffListWidget _staffListWidget;
    private readonly VenueWindowPosition _position;
    private readonly ReloadMods _reloadMods;
    private readonly DisableMods _disableMods;

    public VenueWindow(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        VenueSettings venueSettings,
        SyncFileService syncFileService,
        GuestListWidget guestListWidget,
        StaffListWidget staffListWidget,
        StateService stateService,
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

    private void DrawLiveStream(VenueState venue)
    {
        var liveStream = venue.streams.FirstOrDefault();
        if (liveStream == null) return;

        ImGui.SetCursorPosX((SidebarWidth - StreamButtonWidth) / 2);

        var buttonText = $"{liveStream.name} LIVE";
        var tooltip = $"{liveStream.title}\n{liveStream.viewers} viewers";
        
        DrawStyledButton(buttonText, VenueColors.TwitchButton, VenueColors.TwitchButtonHover, 
            new Vector2(StreamButtonWidth, ButtonHeight), 
            () => Util.OpenLink($"https://twitch.tv/{liveStream.name}"), 
            tooltip);

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

        foreach (var mod in venue.location.mods)
        {
            DrawModCheckbox(mod);
        }

        ImGui.EndChild();
    }

    private void DrawModCheckbox(MannequinModItem mod)
    {
        ImGui.PushID(mod.id);

        bool isEnabled = _configuration.AutoloadMods
            ? !_venueSettings.InactiveMods.Contains(mod.mannequin_id)
            : _venueSettings.ActiveMods.Contains(mod.mannequin_id);

        if (ImGui.Checkbox($"##modToggle{mod.id}", ref isEnabled))
        {
            UpdateModState(mod.mannequin_id, isEnabled);
            _venueSettings.Save();
            _reloadMods.Invoke();
        }

        ImGui.SameLine();
        ImGui.TextWrapped(mod.name);

        if (!string.IsNullOrEmpty(mod.description) && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(mod.description);
        }

        ImGui.Spacing();
        ImGui.PopID();
    }

    private void UpdateModState(string mannequinId, bool isEnabled)
    {
        if (_configuration.AutoloadMods)
        {
            if (isEnabled)
            {
                _venueSettings.InactiveMods.Remove(mannequinId);
            }
            else
            {
                _venueSettings.InactiveMods.Add(mannequinId);
            }
        }
        else
        {
            if (isEnabled)
            {
                _venueSettings.ActiveMods.Add(mannequinId);
            }
            else
            {
                _venueSettings.ActiveMods.Remove(mannequinId);
            }
        }
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

    private void DrawTags(System.Collections.Generic.List<string> tags)
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

    private void DrawStreamsList(System.Collections.Generic.List<VenueStream> streams)
    {
        if (streams.Count == 0) return;

        ImGui.Spacing();
        ImGui.Text("Live Streams:");
        ImGui.Spacing();

        foreach (var stream in streams)
        {
            var buttonText = stream.live
                ? $"{stream.name} - LIVE ({stream.viewers} viewers)"
                : stream.name;

            var tooltip = stream.live ? $"Streaming: {stream.title}" : null;

            DrawStyledButton(buttonText, VenueColors.TwitchButton, VenueColors.TwitchButtonHover,
                new Vector2(400, 25), () => Util.OpenLink($"https://twitch.tv/{stream.name}"), tooltip);
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
}
