using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using OtterGui.Classes;
using VenueSync.Events;
using VenueSync.Services;
using VenueSync.Services.Api.Venue;
using VenueSync.State;

namespace VenueSync.Ui.Crud.Venue;

public class ManageScheduleWindow : Window, IDisposable
{
    private readonly StateService _stateService;
    private readonly ScheduleApi _scheduleApi;

    private bool _isEditMode;
    private string _venueId = string.Empty;
    private string _scheduleId = string.Empty;

    private int _day; // 0-6
    private string _start = string.Empty; // e.g., 18:00
    private string _end = string.Empty;   // e.g., 22:00
    private string _timezone = string.Empty;
    private bool _isSubmitting;

    private static readonly string[] Days =
    [
        "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
    ];

    private static readonly string[] TimezoneOptions =
    [
        "UTC",
        "PST", "PDT",
        "MST", "MDT",
        "CST", "CDT",
        "EST", "EDT",
        "AKST", "AKDT",
        "HST",
        "AST", "ADT",
        "NST", "NDT",
        "GMT", "BST",
        "CET", "CEST",
        "EET", "EEST",
        "MSK",
        "IST",
        "JST",
        "KST",
        "AEST", "AEDT",
        "ACST", "ACDT",
        "AWST",
        "NZST", "NZDT"
    ];

    private static bool IsValidTimeHHmm(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        return TimeSpan.TryParseExact(s.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out _);
    }

    private static string NormalizeTime(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var s = input.Trim();

        // If exactly 4 digits (e.g., "1800") -> "18:00"
        if (Regex.IsMatch(s, "^\\d{4}$"))
            return $"{s[0]}{s[1]}:{s[2]}{s[3]}";

        // If H:MM or HH:M/M -> pad to 2 digits per part
        var match = Regex.Match(s, "^(\\d{1,2}):(\\d{1,2})$");
        if (match.Success)
        {
            var h = int.Parse(match.Groups[1].Value);
            var m = int.Parse(match.Groups[2].Value);
            if (h is >= 0 and <= 23 && m is >= 0 and <= 59)
                return $"{h:00}:{m:00}";
        }

        return s;
    }

    public ManageScheduleWindow(StateService stateService, ScheduleApi scheduleApi, ServiceDisconnected serviceDisconnected) : base("Manage Schedule")
    {
        _stateService = stateService;
        _scheduleApi = scheduleApi;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 220),
            MaximumSize = new Vector2(900, 600),
        };

        RespectCloseHotkey = true;
        IsOpen = false;

        serviceDisconnected.Subscribe(OnDisconnect, ServiceDisconnected.Priority.High);
    }

    private void OnDisconnect()
    {
        if (IsOpen)
        {
            Toggle();
        }
    }

    public void OpenForCreate(string venueId)
    {
        _isEditMode = false;
        _venueId = venueId;
        _scheduleId = string.Empty;
        _day = 0;
        _start = string.Empty;
        _end = string.Empty;
        _timezone = string.Empty;
        IsOpen = true;
    }

    public void OpenForEdit(string venueId, UserVenueScheduleItem schedule)
    {
        _isEditMode = true;
        _venueId = venueId;
        _scheduleId = schedule.id;
        _day = schedule.day;
        _start = schedule.start_time;
        _end = schedule.end_time;
        _timezone = schedule.timezone;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        WindowName = _isEditMode ? "Edit Schedule###ManageScheduleWindow" : "Create Schedule###ManageScheduleWindow";
    }

    public override void Draw()
    {
        ImGui.TextUnformatted(_isEditMode ? "Edit schedule" : "Create a new schedule");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Day");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var dayIdx = _day;
        if (ImGui.Combo("##ScheduleDay", ref dayIdx, Days, Days.Length))
            _day = dayIdx;

        ImGui.TextUnformatted("Open (HH:mm)");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var startBuf = _start;
        if (ImGui.InputText("##ScheduleStart", ref startBuf, 16))
        {
            var normalized = NormalizeTime(startBuf);
            _start = normalized;
        }
        var startValid = IsValidTimeHHmm(_start);
        if (!string.IsNullOrWhiteSpace(_start) && !startValid)
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "Invalid time. Use HH:mm (e.g., 18:00)");

        ImGui.TextUnformatted("Close (HH:mm)");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var endBuf = _end;
        if (ImGui.InputText("##ScheduleEnd", ref endBuf, 16))
        {
            var normalizedEnd = NormalizeTime(endBuf);
            _end = normalizedEnd;
        }
        var endValid = IsValidTimeHHmm(_end);
        if (!string.IsNullOrWhiteSpace(_end) && !endValid)
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "Invalid time. Use HH:mm (e.g., 22:00)");

        ImGui.TextUnformatted("Timezone");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var tzIndex = Math.Max(0, Array.IndexOf(TimezoneOptions, string.IsNullOrWhiteSpace(_timezone) ? "UTC" : _timezone));
        if (ImGui.Combo("##ScheduleTimezone", ref tzIndex, TimezoneOptions, TimezoneOptions.Length))
            _timezone = TimezoneOptions[tzIndex];

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var canSubmit = !_isSubmitting && !string.IsNullOrWhiteSpace(_venueId)
                        && !string.IsNullOrWhiteSpace(_start) && !string.IsNullOrWhiteSpace(_end)
                        && IsValidTimeHHmm(_start) && IsValidTimeHHmm(_end);
        if (!canSubmit) ImGui.BeginDisabled();
        if (ImGui.Button(_isEditMode ? "Update Schedule" : "Create Schedule"))
        {
            _isSubmitting = true;
            _ = SubmitAsync();
            IsOpen = false;
        }
        if (!canSubmit) ImGui.EndDisabled();
    }

    private async Task SubmitAsync()
    {
        var payload = new System.Collections.Generic.Dictionary<string, object?>()
        {
            ["day"] = _day,
            ["start_time"] = _start,
            ["end_time"] = _end,
            ["timezone"] = _timezone,
        };

        try
        {
            ApiResult<UserVenueScheduleItem> result;
            if (_isEditMode)
            {
                var scheduleId = _scheduleId;
                result = await _scheduleApi.UpdateAsync(_venueId, scheduleId, payload).ConfigureAwait(false);
            }
            else
            {
                result = await _scheduleApi.StoreAsync(_venueId, payload).ConfigureAwait(false);
            }

            if (result is { Success: true, Data: not null })
            {
                var scheduleItem = result.Data;
                var venues = _stateService.UserState.venues;
                var venue = venues.FirstOrDefault(v => v.id == _venueId);
                if (venue is not null)
                {
                    var list = venue.schedules;
                    var idx = list.FindIndex(s => s.id == scheduleItem.id);
                    if (idx >= 0)
                        list[idx] = scheduleItem;
                    else
                        list.Add(scheduleItem);
                }

                _scheduleId = scheduleItem.id;

                VenueSync.Messager.NotificationMessage(_isEditMode ? "Schedule updated successfully" : "Schedule created successfully", NotificationType.Success);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Schedule save failed", NotificationType.Error);
                VenueSync.Log.Warning($"Schedule submit failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Schedule save failed", NotificationType.Error);
            VenueSync.Log.Warning($"Schedule submit exception: {ex.Message}");
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    public void Dispose()
    {
        if (IsOpen)
        {
            Toggle();
        }
    }
}
