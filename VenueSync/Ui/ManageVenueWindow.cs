using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiNotification;
using OtterGui.Classes;
using VenueSync.Services;
using VenueSync.Services.Api;
using VenueSync.State;

namespace VenueSync.Ui;

public class ManageVenueWindow : Window, IDisposable
{
    private readonly StateService _stateService;
    private readonly VenueApi _venueApi;

    private bool _isEditMode;
    private string _venueId = string.Empty;

    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _discordInvite = string.Empty;
    private string _carrdUrl = string.Empty;
    private string _tagsCsv = string.Empty;

    private bool _isSubmitting;

    public ManageVenueWindow(StateService stateService, VenueApi venueApi) : base("Manage Venue")
    {
        _stateService = stateService;
        _venueApi = venueApi;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 420),
            MaximumSize = new Vector2(1200, 900),
        };

        RespectCloseHotkey = true;
        IsOpen = false;
    }

    public void OpenForCreate()
    {
        _isEditMode = false;
        _venueId = string.Empty;

        _name = string.Empty;
        _description = string.Empty;
        _discordInvite = string.Empty;
        _carrdUrl = string.Empty;
        _tagsCsv = string.Empty;

        IsOpen = true;
    }

    public void OpenForEdit(UserVenueItem venue)
    {
        _isEditMode = true;
        _venueId = venue.id;

        _name = venue.name;
        _discordInvite = venue.discord_invite;
        _description = venue.description;
        _carrdUrl = venue.carrd_url;
        _tagsCsv = string.Join(", ", venue.tags);

        IsOpen = true;
    }

    public override void PreDraw()
    {
        WindowName = _isEditMode ? "Edit Venue###ManageVenueWindow" : "Create Venue###ManageVenueWindow";
    }

    public override void Draw()
    {
        ImGui.TextUnformatted(_isEditMode ? "Edit an existing venue" : "Create a new venue");
        ImGui.Separator();
        ImGui.Spacing();

        DrawGeneralSection();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var disabled = _isSubmitting || string.IsNullOrWhiteSpace(_name);
        if (disabled)
            ImGui.BeginDisabled();

        if (ImGui.Button(_isEditMode ? "Update Venue" : "Create Venue"))
        {
            _isSubmitting = true;
            _ = SubmitAsync();
            IsOpen = false;
        }

        if (disabled)
            ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            IsOpen = false;
        }
    }

    private void DrawGeneralSection()
    {
        DrawTextInput("Name", ref _name, 256, "Enter the venue name");
        DrawTextInput("Discord Invite", ref _discordInvite, 256, "Discord invite URL or code");
        DrawTextInput("Carrd URL", ref _carrdUrl, 512, "Carrd or other site URL");

        DrawMultilineInput("Description", ref _description, 2000, new Vector2(-1, 100));

        DrawTextInput("Tags (comma separated)", ref _tagsCsv, 512, "Example: music, rp, club");
    }

    private void DrawTextInput(string label, ref string value, int maxLength, string? hint = null)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);

        var buffer = value;
        if (!string.IsNullOrEmpty(hint))
        {
            if (ImGui.InputTextWithHint($"##{label}", hint, ref buffer, maxLength))
            {
                value = buffer;
            }
        }
        else
        {
            if (ImGui.InputText($"##{label}", ref buffer, maxLength))
            {
                value = buffer;
            }
        }
    }

    private void DrawMultilineInput(string label, ref string value, int maxLength, Vector2 size)
    {
        ImGui.TextUnformatted(label);
        var buffer = value;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextMultiline($"##{label}", ref buffer, maxLength, size))
        {
            value = buffer;
        }
    }

    private async Task SubmitAsync()
    {
        var tags = _tagsCsv
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        var payload = new
        {
            name = _name,
            description = _description,
            discord_invite = _discordInvite,
            carrd_url = _carrdUrl,
            tags
        };

        try
        {
            ApiResult<UserVenueItem> result;
            if (_isEditMode && !string.IsNullOrWhiteSpace(_venueId))
            {
                result = await _venueApi.UpdateAsync(_venueId, payload).ConfigureAwait(false);
            }
            else
            {
                result = await _venueApi.StoreAsync(payload).ConfigureAwait(false);
            }

            if (result is { Success: true, Data: not null })
            {
                var venues = _stateService.UserState.venues;
                var updated = result.Data;
                var idx = venues.FindIndex(v => v.id == updated.id);
                if (idx >= 0)
                    venues[idx] = updated;
                else
                    venues.Add(updated);

                VenueSync.Messager.NotificationMessage(_isEditMode ? "Venue updated successfully" : "Venue created successfully", NotificationType.Success);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Venue save failed", NotificationType.Error);
                VenueSync.Log.Warning($"Venue submit failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Venue save failed", NotificationType.Error);
            VenueSync.Log.Warning($"Venue submit exception: {ex.Message}");
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    public void Dispose()
    {
        // no-op
    }
}
