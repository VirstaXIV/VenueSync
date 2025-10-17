using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VenueSync.State;
using System.Linq;

namespace VenueSync.Services;

public class DownloadProgress
{
    public string FileId { get; set; } = "";
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public CancellationTokenSource? CancellationToken { get; init; } = new();
}

public class StorageInfo
{
    public long TotalBytes { get; set; }
    public int FileCount { get; set; }
    public long AvailableBytes { get; set; }
    public bool IsOverLimit => TotalBytes > AvailableBytes;
}

public class SyncFileService : IDisposable
{
    private const string accessTimeMetadataFile = "access_times.json";
    
    private readonly Configuration _configuration;
    private readonly ConcurrentDictionary<string, DownloadProgress> _activeDownloads = new();
    private readonly SemaphoreSlim _downloadSemaphore = new(3);
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _cleanupLock = new(1, 1);
    
    private long _totalBytesAcrossAllFiles = 0;
    private long _downloadedBytesAcrossAllFiles = 0;
    private readonly object _progressLock = new();
    
    private Dictionary<string, DateTime> _fileAccessTimes = new();
    
    public IReadOnlyDictionary<string, DownloadProgress> ActiveDownloads => _activeDownloads;
    public bool IsDownloading => !_activeDownloads.IsEmpty;
    
    public double OverallDownloadProgress 
    { 
        get
        {
            lock (_progressLock)
            {
                return _totalBytesAcrossAllFiles == 0 
                    ? 0 
                    : ((double)_downloadedBytesAcrossAllFiles / _totalBytesAcrossAllFiles) * 100.0;
            }
        }
    }
    
    public string OverallDownloadProgressString
    {
        get
        {
            lock (_progressLock)
            {
                if (_totalBytesAcrossAllFiles == 0)
                    return "0% - 0 MB / 0 MB";

                double downloadedMB = _downloadedBytesAcrossAllFiles / 1024.0 / 1024.0;
                double totalMB = _totalBytesAcrossAllFiles / 1024.0 / 1024.0;
                double percent = OverallDownloadProgress;

                return $"{percent:F1}% - {downloadedMB:F1} MB / {totalMB:F1} MB";
            }
        }
    }
    
    public int ActiveDownloadCount => _activeDownloads.Count;
    
    public SyncFileService(Configuration configuration)
    {
        _httpClient = new HttpClient();
        _configuration = configuration;
        LoadAccessTimes();
    }

    public void Dispose()
    {
        CancelAllDownloads();
        SaveAccessTimes();
        _httpClient?.Dispose();
        _downloadSemaphore?.Dispose();
        _cleanupLock?.Dispose();
    }

    #region File Verification
    
    public bool VerifyFile(string name, string extension, string hash)
    {
        VenueSync.Log.Debug($"Verifying mod file: {name}.{extension} {hash}");
        
        string localPath = GetLocalFilePath(name, extension);
            
        if (!File.Exists(localPath))
            return false;

        if (!string.IsNullOrEmpty(hash) && !VerifyFileHash(localPath, hash))
            return false;
        
        UpdateFileAccessTime(localPath);
        return true;
    }
    
    public bool VerifyModFiles(List<MannequinModItem> mods)
    {
        if (_activeDownloads.Count > 0)
            return false;

        foreach (var mod in mods)
        {
            if (!VerifyFile(mod.id, mod.extension, mod.hash))
                return false;
            
            foreach (var file in mod.files)
            {
                if (!VerifyFile(file.id, file.extension, file.hash))
                    return false;
            }
        }

        return true;
    }
    
    private bool VerifyFileHash(string filePath, string expectedHash)
    {
        try
        {
            var compare = ComputeStreamHash(filePath);
            VenueSync.Log.Debug($"Verifying file hash: {compare} to {expectedHash}");
            return compare.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Failed to verify hash for {filePath}: {ex.Message}");
            return false;
        }
    }
    
    #endregion

    #region File Download
    
    public void MaybeDownloadFile(string id, string file, string extension, string hash, Action<string>? callback = null)
    {
        if (!_activeDownloads.ContainsKey(id))
        {
            if (!VerifyFile(id, extension, hash))
            {
                VenueSync.Log.Debug($"Downloading file: {id}.{extension} {hash}");
                _ = Task.Run(() => DownloadFileAsync(id, file, GetLocalFilePath(id, extension), callback));
            }
            else
            {
                callback?.Invoke(GetLocalFilePath(id, extension));
            }
        }
    }
    
    public void DownloadModFiles(List<MannequinModItem> mods)
    {
        foreach (var mod in mods)
        {
            MaybeDownloadFile(mod.id, mod.file, mod.extension, mod.hash);

            foreach (var file in mod.files)
            {
                MaybeDownloadFile(file.id, file.file, file.extension, file.hash);
            }
        }
    }
    
    private async Task DownloadFileAsync(string id, string file, string localPath, Action<string>? onComplete = null)
    {
        var progress = new DownloadProgress
        {
            FileId = id,
            CancellationToken = new CancellationTokenSource()
        };

        _activeDownloads[id] = progress;

        try
        {
            await _downloadSemaphore.WaitAsync(progress.CancellationToken.Token);
            
            // Check if we need to free up space before downloading
            if (_configuration.AutoCleanupEnabled)
            {
                await EnsureStorageSpace();
            }
            
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            
            VenueSync.Log.Debug($"Downloading mod {id} from: {file}");
            
            using var response = await _httpClient.GetAsync(file, HttpCompletionOption.ResponseHeadersRead, 
                                                            progress.CancellationToken.Token);
            response.EnsureSuccessStatusCode();

            long fileSize = response.Content.Headers.ContentLength ?? 0;
            progress.TotalBytes = fileSize;

            lock (_progressLock)
            {
                _totalBytesAcrossAllFiles += fileSize;
            }

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 
                8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, 
                progress.CancellationToken.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, progress.CancellationToken.Token);
                totalRead += bytesRead;
                
                progress.DownloadedBytes = totalRead;

                lock (_progressLock)
                {
                    _downloadedBytesAcrossAllFiles += bytesRead;
                }
            }
            
            await fileStream.FlushAsync(progress.CancellationToken.Token);

            UpdateFileAccessTime(localPath);
            VenueSync.Log.Debug($"Successfully downloaded: {id} ({OverallDownloadProgress:F1}% overall)");
            
            onComplete?.Invoke(localPath);
        }
        catch (OperationCanceledException)
        {
            VenueSync.Log.Debug($"Download cancelled: {id}");
            lock (_progressLock)
            {
                _totalBytesAcrossAllFiles -= progress.TotalBytes;
                _downloadedBytesAcrossAllFiles -= progress.DownloadedBytes;
            }
            DeleteFile(localPath);
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Failed to download {id}: {exception.Message}");
            lock (_progressLock)
            {
                _totalBytesAcrossAllFiles -= progress.TotalBytes;
                _downloadedBytesAcrossAllFiles -= progress.DownloadedBytes;
            }
            DeleteFile(localPath);
        }
        finally
        {
            _downloadSemaphore.Release();
            _activeDownloads.TryRemove(id, out _);
            progress.CancellationToken?.Dispose();
            
            if (_activeDownloads.Count == 0)
            {
                lock (_progressLock)
                {
                    _totalBytesAcrossAllFiles = 0;
                    _downloadedBytesAcrossAllFiles = 0;
                }
            }
        }
    }

    public void CancelAllDownloads()
    {
        VenueSync.Log.Debug($"Cancelling all downloads.");
        try
        {
            foreach (var download in _activeDownloads.Values)
            {
                download.CancellationToken?.Cancel();
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Debug($"Cancelled downloads failed: {exception.Message}");
        }
    }
    
    #endregion

    #region Storage Management
    
    public StorageInfo GetStorageInfo()
    {
        if (string.IsNullOrEmpty(_configuration.SyncFolder) || !Directory.Exists(_configuration.SyncFolder))
        {
            return new StorageInfo 
            { 
                TotalBytes = 0, 
                FileCount = 0,
                AvailableBytes = _configuration.MaxStorageSizeBytes 
            };
        }

        var files = Directory.GetFiles(_configuration.SyncFolder);
        long totalSize = files.Sum(f => new FileInfo(f).Length);
        
        return new StorageInfo
        {
            TotalBytes = totalSize,
            FileCount = files.Length == 0 ? 0 : files.Length - 1,
            AvailableBytes = _configuration.MaxStorageSizeBytes
        };
    }
    
    public async Task CleanOldFiles()
    {
        await _cleanupLock.WaitAsync();
        try
        {
            if (string.IsNullOrEmpty(_configuration.SyncFolder) || !Directory.Exists(_configuration.SyncFolder))
                return;

            var cutoffDate = DateTime.Now.AddDays(-_configuration.FileRetentionDays);
            var deletedCount = 0;
            var freedBytes = 0L;

            var files = Directory.GetFiles(_configuration.SyncFolder)
                .Where(f => !Path.GetFileName(f).Equals(accessTimeMetadataFile))
                .ToList();

            foreach (var filePath in files)
            {
                var lastAccess = GetFileAccessTime(filePath);
                if (lastAccess < cutoffDate)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var size = fileInfo.Length;
                        fileInfo.Delete();
                        _fileAccessTimes.Remove(filePath);
                        deletedCount++;
                        freedBytes += size;
                        VenueSync.Log.Debug($"Deleted old file: {Path.GetFileName(filePath)} (last accessed: {lastAccess})");
                    }
                    catch (Exception ex)
                    {
                        VenueSync.Log.Error($"Failed to delete file {filePath}: {ex.Message}");
                    }
                }
            }

            if (deletedCount > 0)
            {
                SaveAccessTimes();
                VenueSync.Log.Information($"Cleaned up {deletedCount} old files, freed {freedBytes / 1024.0 / 1024.0:F2} MB");
            }
        }
        finally
        {
            _cleanupLock.Release();
        }
    }
    
    public async Task ClearAllFiles()
    {
        await _cleanupLock.WaitAsync();
        try
        {
            if (string.IsNullOrEmpty(_configuration.SyncFolder) || !Directory.Exists(_configuration.SyncFolder))
                return;

            var files = Directory.GetFiles(_configuration.SyncFolder)
                .Where(f => !Path.GetFileName(f).Equals(accessTimeMetadataFile))
                .ToList();

            var deletedCount = 0;
            var freedBytes = 0L;

            foreach (var filePath in files)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var size = fileInfo.Length;
                    fileInfo.Delete();
                    _fileAccessTimes.Remove(filePath);
                    deletedCount++;
                    freedBytes += size;
                }
                catch (Exception ex)
                {
                    VenueSync.Log.Error($"Failed to delete file {filePath}: {ex.Message}");
                }
            }

            SaveAccessTimes();
            VenueSync.Log.Information($"Cleared all files: {deletedCount} files deleted, freed {freedBytes / 1024.0 / 1024.0:F2} MB");
        }
        finally
        {
            _cleanupLock.Release();
        }
    }
    
    private async Task EnsureStorageSpace()
    {
        var storageInfo = GetStorageInfo();
        
        if (!storageInfo.IsOverLimit)
            return;

        await _cleanupLock.WaitAsync();
        try
        {
            VenueSync.Log.Information($"Storage limit reached. Current: {storageInfo.TotalBytes / 1024.0 / 1024.0:F2} MB, Limit: {storageInfo.AvailableBytes / 1024.0 / 1024.0:F2} MB");

            if (string.IsNullOrEmpty(_configuration.SyncFolder) || !Directory.Exists(_configuration.SyncFolder))
                return;

            var files = Directory.GetFiles(_configuration.SyncFolder)
                .Where(f => !Path.GetFileName(f).Equals(accessTimeMetadataFile))
                .Select(f => new FileInfo(f))
                .OrderBy(f => GetFileAccessTime(f.FullName))
                .ToList();

            long currentSize = storageInfo.TotalBytes;
            var deletedCount = 0;

            foreach (var fileInfo in files)
            {
                if (currentSize <= _configuration.MaxStorageSizeBytes)
                    break;

                try
                {
                    var size = fileInfo.Length;
                    fileInfo.Delete();
                    _fileAccessTimes.Remove(fileInfo.FullName);
                    currentSize -= size;
                    deletedCount++;
                    VenueSync.Log.Debug($"Deleted old file to free space: {fileInfo.Name}");
                }
                catch (Exception ex)
                {
                    VenueSync.Log.Error($"Failed to delete file {fileInfo.FullName}: {ex.Message}");
                }
            }

            if (deletedCount > 0)
            {
                SaveAccessTimes();
                VenueSync.Log.Information($"Freed space by deleting {deletedCount} oldest files");
            }
        }
        finally
        {
            _cleanupLock.Release();
        }
    }
    
    #endregion

    #region File Access Tracking
    
    private void UpdateFileAccessTime(string filePath)
    {
        _fileAccessTimes[filePath] = DateTime.Now;
    }
    
    private DateTime GetFileAccessTime(string filePath)
    {
        if (_fileAccessTimes.TryGetValue(filePath, out var accessTime))
            return accessTime;
        
        // Fallback to file system last access time
        if (File.Exists(filePath))
            return File.GetLastAccessTime(filePath);
        
        return DateTime.MinValue;
    }
    
    private void LoadAccessTimes()
    {
        if (string.IsNullOrEmpty(_configuration.SyncFolder))
            return;

        var metadataPath = Path.Combine(_configuration.SyncFolder, accessTimeMetadataFile);
        
        if (!File.Exists(metadataPath))
            return;

        try
        {
            var json = File.ReadAllText(metadataPath);
            _fileAccessTimes = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json) 
                ?? new Dictionary<string, DateTime>();
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Failed to load access times: {ex.Message}");
            _fileAccessTimes = new Dictionary<string, DateTime>();
        }
    }
    
    private void SaveAccessTimes()
    {
        if (string.IsNullOrEmpty(_configuration.SyncFolder))
            return;

        try
        {
            Directory.CreateDirectory(_configuration.SyncFolder);
            var metadataPath = Path.Combine(_configuration.SyncFolder, accessTimeMetadataFile);
            var json = System.Text.Json.JsonSerializer.Serialize(_fileAccessTimes, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(metadataPath, json);
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Failed to save access times: {ex.Message}");
        }
    }
    
    #endregion

    #region Helper Methods
    
    public Dictionary<string, string> BuildModFileList(List<string> paths, List<MannequinModItem> mods)
    {
        var filePaths = new Dictionary<string, string>();

        var index = 0;
        foreach (var mod in mods)
        {
            filePaths.Add(paths[index], GetLocalFilePath(mod.id, mod.extension));
            foreach (var file in mod.files)
            {
                filePaths.Add(file.path, GetLocalFilePath(file.id, file.extension));
            }

            index += 1;
        }

        return filePaths;
    }
    
    private string GetLocalFilePath(string fileId, string extension)
    {
        string hashedName = ComputeHash(fileId);
        return Path.Combine(_configuration.SyncFolder, $"{hashedName}.{extension}");
    }
    
    private void DeleteFile(string localPath)
    {
        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                _fileAccessTimes.Remove(localPath);
            }
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Failed to delete file {localPath}: {ex.Message}");
        }
    }
    
    private string ComputeHash(string input)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
    
    private string ComputeStreamHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        byte[] hashBytes = SHA256.HashData(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
    
    #endregion
}