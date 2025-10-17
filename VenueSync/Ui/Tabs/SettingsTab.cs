using System;
using System.IO;
using System.Linq;
using System.Numerics;
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

namespace VenueSync.Ui.Tabs;

public class SettingsTab(Configuration configuration, StateService stateService, SyncFileService syncFileService,
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
    
    private bool IsClientMode()
    {
        return !stateService.Connection.Connected || configuration.ClientMode;
    }

    public void DrawContent()
    {
        using var child = ImUtf8.Child("MainWindowChild"u8, default);
        if (!child)
            return;

        if (IsClientMode())
        {
            DrawSettingsWithTabs();
        }
        else
        {
            DrawSettingsWithHeaders();
        }
    }

    private void DrawSettingsWithTabs()
    {
        using var tabBar = ImUtf8.TabBar("SettingsTabBar"u8);
        if (!tabBar)
            return;

        using (var tabItem = ImUtf8.TabItem("Service"u8))
        {
            if (tabItem)
            {
                using var child = ImUtf8.Child("ServiceChild"u8, default);
                if (child)
                    DrawServiceSettingsContent();
            }
        }

        using (var tabItem = ImUtf8.TabItem("Venues"u8))
        {
            if (tabItem)
            {
                using var child = ImUtf8.Child("VenuesChild"u8, default);
                if (child)
                    DrawVenueSettingsContent();
            }
        }

        using (var tabItem = ImUtf8.TabItem("Files"u8))
        {
            if (tabItem)
            {
                using var child = ImUtf8.Child("FilesChild"u8, default);
                if (child)
                    DrawFilesystemSettingsContent();
            }
        }

        using (var tabItem = ImUtf8.TabItem("Interface"u8))
        {
            if (tabItem)
            {
                using var child = ImUtf8.Child("InterfaceChild"u8, default);
                if (child)
                    DrawInterfaceSettingsContent();
            }
        }
    }

    private void DrawSettingsWithHeaders()
    {
        using (ImUtf8.Child("SettingsChild"u8, default))
        {
            DrawServiceSettings();
            DrawVenueSettings();
            DrawFilesystemSettings();
            DrawInterfaceSettings();
        }
    }

    private void DrawServiceSettings()
    {
        if (!ImUtf8.CollapsingHeader("Service"u8))
            return;

        DrawServiceSettingsContent();
    }

    private void DrawServiceSettingsContent()
    {
        if (configuration.ServerToken.IsNullOrEmpty())
        {
            DrawAuthenticationSection();
        }
        else
        {
            DrawConnectedSection();
        }
    }

    private void DrawAuthenticationSection()
    {
        ImGui.TextWrapped("Connect with XIVAuth to sync venue data across characters and access the VenueSync dashboard.");
        ImGui.Spacing();
        
        ImGui.BeginDisabled(_currentlyRegistering);
        if (ImUtf8.Button("Connect with XIVAuth"u8))
        {
            HandleXIVAuthConnection();
        }
        ImGui.EndDisabled();

        if (_currentlyRegistering)
        {
            ImGui.TextUnformatted("Waiting for the server...");
        }
        else if (!_registerMessage.IsNullOrEmpty())
        {
            var color = _registered ? ImGui.GetColorU32(ImGuiCol.Text) : ImGui.GetColorU32(ImGuiColors.DalamudYellow);
            ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(color), _registerMessage);
        }
    }

    private void DrawConnectedSection()
    {
        ImGui.TextWrapped("The VenueSync Dashboard lets you add and manage venues. It's optional—you can visit venues without using it.");
        ImGui.Spacing();
        
        if (ImUtf8.Button("Open VenueSync Dashboard"u8))
        {
            Util.OpenLink(Configuration.Constants.VenueSyncDashboard);
        }
        
        ImGui.Separator();
        
        Checkbox("Auto Connect"u8, "Automatically connect to VenueSync when possible."u8, 
            configuration.AutoConnect, v => configuration.AutoConnect = v);
        
        ImGui.Separator();
        
        DrawConnectionControls();
        
        ImGui.Separator();
        
        ImGui.TextWrapped("Removing your token will require re-authentication. This can help resolve connection issues.");
        ImGui.Spacing();
        
        if (ImUtf8.Button("Remove Token"u8))
        {
            HandleTokenRemoval();
        }
    }

    private void DrawConnectionControls()
    {
        ImUtf8.Text("Service Connection"u8);
        
        if (stateService.Connection.Connected)
        {
            ImGui.BeginDisabled(_isDisconnecting);
            if (ImUtf8.Button("Disconnect from Service"u8))
            {
                HandleDisconnect();
            }
            ImGui.EndDisabled();
            
            if (_isDisconnecting)
            {
                ImGui.SameLine();
                ImGui.TextUnformatted("Disconnecting...");
            }
        }
        else
        {
            ImGui.BeginDisabled(_isConnecting);
            if (ImUtf8.Button("Connect to Service"u8))
            {
                HandleConnect();
            }
            ImGui.EndDisabled();
            
            if (_isConnecting)
            {
                ImGui.SameLine();
                ImGui.TextUnformatted("Connecting...");
            }
        }
    }
    
    private void DrawFilesystemSettings()
    {
        if (!ImUtf8.CollapsingHeader("Files"u8))
            return;

        DrawFilesystemSettingsContent();
    }

    private void DrawFilesystemSettingsContent()
    {
        ImGui.TextWrapped("Select a folder to store synced venue data. Avoid using OneDrive or Penumbra directories.");
        ImGui.Spacing();
        
        var defaultFolderName = @"C:\FFXIVVenueSync";
        var syncFolder = configuration.SyncFolder == string.Empty ? defaultFolderName : configuration.SyncFolder;
        
        ImGui.SetNextItemWidth(-60); // Leave room for button
        ImGui.InputText("##StorageFolder", ref syncFolder, 255, ImGuiInputTextFlags.ReadOnly);
        
        ImGui.SameLine();
        if (ImUtf8.IconButton(FontAwesomeIcon.Folder, "Browse"u8))
        {
            OpenFolderDialog();
        }
        
        if (!_fileSelectError.IsNullOrEmpty())
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, $"Error: {_fileSelectError}");
        }
        
        if (_dialogIsOpen)
        {
            fileDialogManager.Draw();
        }

        // Storage Management Section
        if (ImGui.CollapsingHeader("Storage Management"))
        {
            var storageInfo = syncFileService.GetStorageInfo();
            var usedGB = storageInfo.TotalBytes / 1024.0 / 1024.0 / 1024.0;
            var maxGB = configuration.MaxStorageSizeBytes / 1024.0 / 1024.0 / 1024.0;
            
            ImGui.Text($"Storage Used: {usedGB:F2} GB / {maxGB:F2} GB ({storageInfo.FileCount} files)");
            ImGui.ProgressBar((float)(storageInfo.TotalBytes / (double)configuration.MaxStorageSizeBytes), 
                new Vector2(-1, 0), $"{usedGB:F2} GB / {maxGB:F2} GB");
            
            ImGui.Spacing();
            
            // Max storage size
            var maxStorageGB = (int)(maxGB);
            if (ImGui.SliderInt("Max Storage Size (GB)", ref maxStorageGB, 1, 100))
            {
                configuration.MaxStorageSizeBytes = maxStorageGB * 1024L * 1024L * 1024L;
                configuration.Save();
            }
            
            ImGui.Spacing();
            
            // File retention days
            var retentionDays = configuration.FileRetentionDays;
            if (ImGui.SliderInt("Delete Files After (Days)", ref retentionDays, 1, 365))
            {
                configuration.FileRetentionDays = retentionDays;
                configuration.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Files not accessed for this many days will be deleted during cleanup");
            }
            
            ImGui.Spacing();
            
            // Auto cleanup toggle
            var autoCleanup = configuration.AutoCleanupEnabled;
            if (ImGui.Checkbox("Auto Cleanup During Downloads", ref autoCleanup))
            {
                configuration.AutoCleanupEnabled = autoCleanup;
                configuration.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Automatically delete old files when storage limit is reached during downloads");
            }
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Action buttons
            if (ImGui.Button("Scan & Clean Old Files"))
            {
                _ = Task.Run(async () => await syncFileService.CleanOldFiles());
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Delete files not accessed in the last {configuration.FileRetentionDays} days");
            }
            
            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();
            
            if (ImGui.Button("Clear All Files"))
            {
                ImGui.OpenPopup("ConfirmClearAll");
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Delete ALL files in the sync folder");
            }
            
            // Confirmation popup
            if (ImGui.BeginPopupModal("ConfirmClearAll", ref _showClearAllPopup, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Are you sure you want to delete all {storageInfo.FileCount} files?");
                ImGui.Text($"This will free up {usedGB:F2} GB of storage.");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                if (ImGui.Button("Yes, Delete All", new Vector2(120, 0)))
                {
                    _ = Task.Run(async () => await syncFileService.ClearAllFiles());
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        // ... existing code ...
    }

    private bool _showClearAllPopup = true;

    private void DrawInterfaceSettings()
    {
        if (!ImUtf8.CollapsingHeader("Interface"u8))
            return;

        DrawInterfaceSettingsContent();
    }

    private void DrawInterfaceSettingsContent()
    {
        Checkbox("Open Main Window at Game Start"u8, 
            "Open the VenueSync window automatically when launching the game."u8,
            configuration.OpenWindowAtStart, v => configuration.OpenWindowAtStart = v);
        
        Checkbox("Enable DTR Bar Entry"u8,
            "Display VenueSync status in the server info bar."u8,
            configuration.EnableDtrBar, v => configuration.EnableDtrBar = v);
        
        Checkbox("Client Mode"u8, 
            "Hide venue management features if you only want to visit venues."u8, 
            configuration.ClientMode, v => configuration.ClientMode = v);
    }
    
    private void DrawVenueSettings()
    {
        if (!ImUtf8.CollapsingHeader("Venues"u8))
            return;

        DrawVenueSettingsContent();
    }

    private void DrawVenueSettingsContent()
    {
        if (!configuration.ClientMode)
        {
            Checkbox("Notify of Entrances."u8,
                     "Give a chat notification when someone enters the venue. Only when not in client mode."u8,
                     configuration.NotifyEntrances, v => configuration.NotifyEntrances = v);
        }
        
        Checkbox("Autoload Mods"u8,
                 "When entering a venue, mods are opt-in. Selecting this will switch to opt-out. Each mode is independent, so what mods are enabled / disabled are not carried over."u8,
                 configuration.AutoloadMods, v => configuration.AutoloadMods = v);
    }
    
    private void HandleXIVAuthConnection()
    {
        _currentlyRegistering = true;
        _registerMessage = null;
        
        _ = Task.Run(async () =>
        {
            try
            {
                var reply = await accountService.XIVAuth(CancellationToken.None).ConfigureAwait(false);
                if (!reply.Success)
                {
                    VenueSync.Log.Warning($"Registration Failed: {reply.ErrorMessage}");
                    _registerMessage = reply.ErrorMessage ?? "An unknown error occurred. Please try again later.";
                    _registered = false;
                    return;
                }
                
                _token = reply.Token ?? string.Empty;
                _userId = reply.UserID ?? string.Empty;
                _registered = true;
                _registerMessage = "Account registered successfully!";
                
                configuration.ServerToken = _token;
                configuration.ServerUserID = _userId;
                configuration.SaveNow();
            }
            catch (Exception ex)
            {
                VenueSync.Log.Warning($"Registration Failed: {ex.Message}");
                _registerMessage = "An unknown error occurred. Please try again later.";
                _registered = false;
            }
            finally
            {
                _currentlyRegistering = false;
            }
        });
    }

    private void HandleTokenRemoval()
    {
        _registerMessage = string.Empty;
        _registered = false;
        configuration.ServerToken = string.Empty;
        configuration.ServerUserID = string.Empty;
        configuration.SaveNow();
        VenueSync.Messager.NotificationMessage("VenueSync Authentication Removed", NotificationType.Info);
    }

    private void HandleConnect()
    {
        _isConnecting = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await socketService.ConnectAsync();
            }
            catch (Exception ex)
            {
                VenueSync.Log.Error($"Connection failed: {ex.Message}");
            }
            finally
            {
                _isConnecting = false;
            }
        });
    }

    private void HandleDisconnect()
    {
        _isDisconnecting = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await socketService.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                VenueSync.Log.Error($"Disconnect failed: {ex.Message}");
            }
            finally
            {
                _isDisconnecting = false;
            }
        });
    }

    private void OpenFolderDialog()
    {
        _dialogIsOpen = true;
        _fileSelectError = string.Empty;
        
        fileDialogManager.OpenFolderDialog("Pick VenueSync Storage Folder", (success, path) =>
        {
            _dialogIsOpen = false;
            
            if (!success) return;

            var validationError = ValidateStorageFolder(path);
            if (validationError != null)
            {
                _fileSelectError = validationError;
                return;
            }

            configuration.SyncFolder = path;
            configuration.Save();
        }, @"C:\");
    }

    private string? ValidateStorageFolder(string path)
    {
        // Check for OneDrive
        if (path.Contains("onedrive", StringComparison.OrdinalIgnoreCase))
            return "OneDrive folders are not supported.";
        
        // Check for Penumbra directory
        if (string.Equals(path.ToLowerInvariant(), ipcManager.Penumbra.ModDirectory?.ToLowerInvariant(), StringComparison.Ordinal))
            return "Cannot use Penumbra mod directory.";
        
        // Check if writable
        if (!IsDirectoryWritable(path))
            return "Directory is not writable.";
        
        // Check for invalid files
        var cacheDirFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        var cacheSubDirs = Directory.GetDirectories(path);
        
        var hasInvalidFiles = cacheDirFiles.Any(f =>
            Path.GetFileNameWithoutExtension(f).Length != 40
            && !Path.GetExtension(f).Equals(".tmp", StringComparison.OrdinalIgnoreCase)
            && !Path.GetExtension(f).Equals(".blk", StringComparison.OrdinalIgnoreCase)
        );

        var hasInvalidDirs = cacheSubDirs
            .Select(f => Path.GetFileName(Path.TrimEndingDirectorySeparator(f)))
            .Any(f => !f.Equals("subst", StringComparison.OrdinalIgnoreCase));

        if (hasInvalidFiles || hasInvalidDirs)
            return "Folder contains invalid files. Please select an empty folder.";
        
        // Validate path format
        if (!Regex.IsMatch(path, @"^(?:[a-zA-Z]:\\[\w\s\-\\]+?|\/(?:[\w\s\-\/])+?)$", RegexOptions.ECMAScript))
            return "Invalid path format.";
        
        // Check if exists
        if (!Directory.Exists(path))
            return "Directory does not exist.";

        return null;
    }
    
    private bool IsDirectoryWritable(string dirPath)
    {
        try
        {
            using FileStream fs = File.Create(
                Path.Combine(dirPath, Path.GetRandomFileName()),
                1,
                FileOptions.DeleteOnClose);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool current, Action<bool> setter)
    {
        using var id = ImUtf8.PushId(label);
        var tmp = current;
        if (ImUtf8.Checkbox(""u8, ref tmp) && tmp != current)
        {
            setter(tmp);
            configuration.Save();
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker(label, tooltip);
    }
}