using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using OtterGui.Classes;
using OtterGui.Text;
using OtterGui.Widgets;
using VenueSync.Services;

namespace VenueSync.Ui.Tabs.SettingsTab;

public class SettingsTab(Configuration configuration, StateService stateService, 
    AccountService accountService, SocketService socketService, FileDialogManager fileDialogManager, IPCManager ipcManager): ITab
{
    private bool _currentlyRegistering = false;
    private bool _registered = false;
    private string? _registerMessage;
    private string _token = string.Empty;
    private string _userId = string.Empty;
    private bool _isConnecting = false;
    private bool _isDisconnecting = false;
    
    private string _fileSelectError = string.Empty;
    private bool _dialogIsOpen = false;
    
    public ReadOnlySpan<byte> Label => "Settings"u8;

    public void DrawContent()
    {
        using var child = ImUtf8.Child("MainWindowChild"u8, default);
        if (!child)
            return;

        using (ImUtf8.Child("SettingsChild"u8, default))
        {
            DrawServiceSettings();
            DrawFilesystemSettings();
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
            ImGui.TextWrapped($"Removing your token will require re-authenticating. This can help if your having connection troubles.");
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
            
            ImGui.TextWrapped($"The VenueSync Dashboard lets you add / manage venues. It is unnecessary to do anything there or validate characters if you are just interested in visiting venues.");
            if (ImUtf8.Button("Open VenueSync Dashboard"u8))
            {
                Util.OpenLink(Configuration.Constants.VenueSyncDashboard);
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
    
    private bool IsDirectoryWritable(string dirPath, bool throwIfFails = false)
    {
        try
        {
            using FileStream fs = File.Create(
                Path.Combine(
                    dirPath,
                    Path.GetRandomFileName()
                ),
                1,
                FileOptions.DeleteOnClose);
            return true;
        }
        catch
        {
            if (throwIfFails)
                throw;

            return false;
        }
    }

    private void DrawFilesystemSettings()
    {
        if (!ImUtf8.CollapsingHeader("Files"u8))
            return;

        var defaultFolderName = @"C:\FFXIVVenueSync";
        var syncFolder = configuration.SyncFolder == string.Empty ? defaultFolderName : configuration.SyncFolder;
        ImGui.InputText("Storage Folder##cache", ref syncFolder, 255, ImGuiInputTextFlags.ReadOnly);
        
        ImGui.SameLine();
        
        if (ImUtf8.IconButton(FontAwesomeIcon.Folder))
        {
            //TODO: Figure out the issue with opening a dialog
            _dialogIsOpen = true;
            fileDialogManager.OpenFolderDialog("Pick VenueSync Storage Folder", (success, path) =>
            {
                if (!success) return;

                var isOneDrive = path.Contains("onedrive", StringComparison.OrdinalIgnoreCase);
                var isWritable = IsDirectoryWritable(path);
                var isPenumbraDirectory = string.Equals(path.ToLowerInvariant(), ipcManager.Penumbra.ModDirectory?.ToLowerInvariant(), StringComparison.Ordinal);
                
                if (isOneDrive || isPenumbraDirectory)
                {
                    _fileSelectError = "Don't use penumbra or onedrive folders.";
                    return;
                }
                
                if (!isWritable)
                {
                    _fileSelectError = "Directory is not writable.";
                    return;
                }
                
                var cacheDirFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                var cacheSubDirs = Directory.GetDirectories(path);
                
                var otherFiles = cacheDirFiles.Any(f =>
                                                       Path.GetFileNameWithoutExtension(f).Length != 40
                                                       && !Path.GetExtension(f).Equals("tmp", StringComparison.OrdinalIgnoreCase)
                                                       && !Path.GetExtension(f).Equals("blk", StringComparison.OrdinalIgnoreCase)
                );

                if (!otherFiles && cacheSubDirs.Select(f => Path.GetFileName(Path.TrimEndingDirectorySeparator(f))).Any(f => !f.Equals("subst", StringComparison.OrdinalIgnoreCase)))
                {
                    _fileSelectError = "Invalid files found in folder.";
                    return;
                }

                if (!Regex.IsMatch(path, @"^(?:[a-zA-Z]:\\[\w\s\-\\]+?|\/(?:[\w\s\-\/])+?)$", RegexOptions.ECMAScript))
                {
                    _fileSelectError = "Invalid path.";
                    return;
                }

                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    configuration.SyncFolder = path;
                    configuration.Save();
                }
                else
                {
                    _fileSelectError = "Invalid path.";
                }
            }, @"C:\");
        }
        
        if (_dialogIsOpen)
        {
            fileDialogManager.Draw();
        }

        if (_fileSelectError != string.Empty)
        {
            ImUtf8.Text("Invalid Path"u8);
        }
    }

    private void DrawInterfaceSettings()
    {
        if (!ImUtf8.CollapsingHeader("Interface"u8))
            return;

        Checkbox("Open Main Window at Game Start"u8, "Whether the main VenueSync window should be open or closed after launching the game."u8,
                 configuration.OpenWindowAtStart, v => configuration.OpenWindowAtStart = v);
        
        Checkbox("Client Mode"u8, "If you are only interested in using VenueSync as a client, this will disable showing unneeded items in the UI."u8, configuration.ClientMode, v => configuration.ClientMode = v);
        
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
