using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using OtterGui.Classes;
using VenueSync.Events;
using VenueSync.Services;
using VenueSync.Services.Api.Venue;
using VenueSync.State;

namespace VenueSync.Ui.Crud.Venue;

public class ManageStreamWindow : Window, IDisposable
{
    private readonly StateService _stateService;
    private readonly StreamApi _streamApi;

    private bool _isEditMode;
    private string _venueId = string.Empty;
    private string _streamId = string.Empty;
    private string _name = string.Empty;
    private string _type = string.Empty;
    private bool _isSubmitting;

    public ManageStreamWindow(StateService stateService, StreamApi streamApi, ServiceDisconnected serviceDisconnected) : base("Manage Stream")
    {
        _stateService = stateService;
        _streamApi = streamApi;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 200),
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
        _streamId = string.Empty;
        _name = string.Empty;
        _type = string.Empty;
        IsOpen = true;
    }

    public void OpenForEdit(string venueId, UserVenueStreamItem stream)
    {
        _isEditMode = true;
        _venueId = venueId;
        _streamId = stream.id;
        _name = stream.name;
        _type = stream.type;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        WindowName = _isEditMode ? "Edit Stream###ManageStreamWindow" : "Create Stream###ManageStreamWindow";
    }

    public override void Draw()
    {
        ImGui.TextUnformatted(_isEditMode ? "Edit stream" : "Create a new stream");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Name");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var nameBuf = _name;
        if (ImGui.InputText("##StreamName", ref nameBuf, 128))
            _name = nameBuf;

        ImGui.TextUnformatted("Type");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var typeBuf = _type;
        if (ImGui.InputText("##StreamType", ref typeBuf, 64))
            _type = typeBuf;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var canSubmit = !_isSubmitting && !string.IsNullOrWhiteSpace(_venueId) && (_isEditMode || !string.IsNullOrWhiteSpace(_name));
        if (!canSubmit) ImGui.BeginDisabled();
        if (ImGui.Button(_isEditMode ? "Update Stream" : "Create Stream"))
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
            ["name"] = _name,
            ["type"] = _type,
        };

        try
        {
            ApiResult<UserVenueStreamItem> result;
            if (_isEditMode)
            {
                var streamId = _streamId;
                result = await _streamApi.UpdateAsync(_venueId, streamId, payload).ConfigureAwait(false);
            }
            else
            {
                result = await _streamApi.StoreAsync(_venueId, payload).ConfigureAwait(false);
            }

            if (result is { Success: true, Data: not null })
            {
                var streamItem = result.Data;
                var venues = _stateService.UserState.venues;
                var venue = venues.FirstOrDefault(v => v.id == _venueId);
                if (venue is not null)
                {
                    var list = venue.streams;
                    var idx = list.FindIndex(s => s.id == streamItem.id);
                    if (idx >= 0)
                        list[idx] = streamItem;
                    else
                        list.Add(streamItem);
                }

                _streamId = streamItem.id;

                VenueSync.Messager.NotificationMessage(_isEditMode ? "Stream updated successfully" : "Stream created successfully", NotificationType.Success);
            }
            else
            {
                VenueSync.Messager.NotificationMessage("Stream save failed", NotificationType.Error);
                VenueSync.Log.Warning($"Stream submit failed: {result.ErrorMessage ?? "Unknown error"}");
            }
        }
        catch (Exception ex)
        {
            VenueSync.Messager.NotificationMessage("Stream save failed", NotificationType.Error);
            VenueSync.Log.Warning($"Stream submit exception: {ex.Message}");
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
