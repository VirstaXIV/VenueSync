using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using OtterGui.Classes;
using OtterGui.Text;
using OtterGui.Widgets;
using VenueSync.Services;

namespace VenueSync.Ui.Tabs.SettingsTab;

public class SettingsTab(Configuration configuration, StateService stateService, AccountService accountService, SocketService socketService): ITab
{
    private bool _currentlyRegistering = false;
    private bool _registered = false;
    private string? _registerMessage;
    private string _token = string.Empty;
    private string _userId = string.Empty;
    private bool _isConnecting = false;
    private bool _isDisconnecting = false;
    
    public ReadOnlySpan<byte> Label => "Settings"u8;

    public void DrawContent()
    {
        using var child = ImUtf8.Child("MainWindowChild"u8, default);
        if (!child)
            return;

        using (ImUtf8.Child("SettingsChild"u8, default))
        {
            DrawServiceSettings();
            DrawInterfaceSettings();
        }
    }

    private void DrawServiceSettings()
    {
        if (!ImUtf8.CollapsingHeader("Service"u8))
            return;

        if (configuration.ServerToken.IsNullOrEmpty())
        {
            ImGui.BeginDisabled(_currentlyRegistering);
            if (ImUtf8.Button("Connect with XIVAuth"u8))
            {
                _currentlyRegistering = true;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var reply = await accountService.XIVAuth(CancellationToken.None).ConfigureAwait(false);
                        if (!reply.Success)
                        {
                            VenueSync.Log.Warning($"Registration Failed: {reply.ErrorMessage}");
                            _registerMessage = reply.ErrorMessage;
                            if (_registerMessage.IsNullOrEmpty())
                                _registerMessage = "An unknown error occured. Please try again later.";
                            return;
                        }
                        _registerMessage = "Account registered.";
                        _token = reply.Token ?? string.Empty;
                        _userId = reply.UserID ?? string.Empty;
                        _registered = true;
                        configuration.ServerToken = _token;
                        configuration.ServerUserID = _userId;
                        configuration.SaveNow();
                    }
                    catch (Exception ex)
                    {
                        VenueSync.Log.Warning($"Registration Failed: {ex.Message}");
                        _registerMessage = "An unknown error occured. Please try again later.";
                        _registered = false;
                    }
                    finally
                    {
                        _currentlyRegistering = false;
                    }
                });
            }
            ImGui.EndDisabled();

            if (_currentlyRegistering)
            {
                ImGui.TextUnformatted("Waiting for the server...");
            }
            else if (!_registerMessage.IsNullOrEmpty())
            {
                if (!_registered)
                    ImGui.TextColored(ImGuiColors.DalamudYellow, _registerMessage);
                else
                    ImGui.TextWrapped(_registerMessage);
            }
        }
        else
        {
            if (ImUtf8.Button("Remove Token"u8))
            {
                _registerMessage = string.Empty;
                _registered = false;
                configuration.ServerToken = string.Empty;
                configuration.ServerUserID = string.Empty;
                configuration.SaveNow();
                VenueSync.Messager.NotificationMessage($"VenueSync Authentication Removed", NotificationType.Info);
            }
            
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            
            Checkbox("Auto Connect"u8, "Should a connection be attempted when possible?"u8, configuration.AutoConnect, v => configuration.AutoConnect = v);
            
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            
            ImUtf8.Text("Connect to the VenueSync Service"u8);

            if (stateService.Connection.Connected)
            {
                ImGui.BeginDisabled(_isDisconnecting);
                if (ImUtf8.Button("Disconnect from Service"u8))
                {
                    _isDisconnecting = true;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await socketService.Disconnect(true);
                            _isDisconnecting = false;
                        }
                        catch (Exception)
                        {
                            // could pass an alert here for user
                            _isDisconnecting = false;
                        }
                    });
                }
                ImGui.EndDisabled();
            }
            else
            {
                ImGui.BeginDisabled(_isConnecting);
                if (ImUtf8.Button("Connect to Service"u8))
                {
                    _isConnecting = true;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await socketService.Connect();
                            _isConnecting = false;
                        }
                        catch (Exception)
                        {
                            // could pass an alert here for user
                            _isConnecting = false;
                        }
                    });
                }
                ImGui.EndDisabled();
            }
        }
    }

    private void DrawInterfaceSettings()
    {
        if (!ImUtf8.CollapsingHeader("Interface"u8))
            return;

        Checkbox("Open Main Window at Game Start"u8, "Whether the main VenueSync window should be open or closed after launching the game."u8,
                 configuration.OpenWindowAtStart, v => configuration.OpenWindowAtStart = v);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool current, Action<bool> setter)
    {
        using var id  = ImUtf8.PushId(label);
        var       tmp = current;
        if (ImUtf8.Checkbox(""u8, ref tmp) && tmp != current)
        {
            setter(tmp);
            configuration.Save();
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker(label, tooltip);
    }
}
