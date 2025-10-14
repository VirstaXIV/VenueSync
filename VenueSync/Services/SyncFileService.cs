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

namespace VenueSync.Services;

public class DownloadProgress
{
    public string FileId { get; set; } = "";
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public CancellationTokenSource? CancellationToken { get; init; } = new();
}

public class SyncFileService: IDisposable
{
    private readonly Configuration _configuration;
    
    private readonly ConcurrentDictionary<string, DownloadProgress> _activeDownloads = new();
    private readonly SemaphoreSlim _downloadSemaphore = new(3); // Limit concurrent downloads
    private readonly HttpClient _httpClient;
    
    private long _totalBytesAcrossAllFiles = 0;
    private long _downloadedBytesAcrossAllFiles = 0;
    private readonly object _progressLock = new object();
    
    public IReadOnlyDictionary<string, DownloadProgress> ActiveDownloads => _activeDownloads;
    public bool IsDownloading => !_activeDownloads.IsEmpty;
    public double OverallDownloadProgress 
    { 
        get
        {
            lock (_progressLock)
            {
                if (_totalBytesAcrossAllFiles == 0)
                    return 0;
            
                return ((double)_downloadedBytesAcrossAllFiles / _totalBytesAcrossAllFiles) * 100.0;
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
    }

    public void Dispose()
    {
        CancelAllDownloads();
    }

    public bool VerifyFile(string name, string extension, string hash)
    {
        VenueSync.Log.Debug($"Verifying mod file: {name}.{extension} {hash}");
        
        string localPath = GetLocalFilePath(name, extension);
            
        if (!File.Exists(localPath))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(hash) && !VerifyFileHash(localPath, hash))
        {
            return false;
        }
        
        return true;
    }
    
    public bool VerifyModFiles(List<MannequinModItem> mods)
    {
        if (_activeDownloads.Count > 0)
        {
            return false;
        }

        foreach (var mod in mods)
        {
            if (!VerifyFile(mod.id, mod.extension, mod.hash))
            {
                return false;
            }
            foreach (var file in mod.files)
            {
                if (!VerifyFile(file.id, file.extension, file.hash))
                {
                    return false;
                }
            }
        }

        return true;
    }
    
    private string GetLocalFilePath(string fileId, string extension)
    {
        string hashedName = ComputeHash(fileId);
        return Path.Combine(_configuration.SyncFolder, $"{hashedName}.{extension}");
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
    
    async private Task DownloadFileAsync(string id, string file, string localPath, Action<string>? onComplete = null)
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
                    _downloadedBytesAcrossAllFiles += (bytesRead);
                }
            }
            
            await fileStream.FlushAsync(progress.CancellationToken.Token);

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
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Error($"Failed to download {id}: {exception.Message}");
            lock (_progressLock)
            {
                _totalBytesAcrossAllFiles -= progress.TotalBytes;
                _downloadedBytesAcrossAllFiles -= progress.DownloadedBytes;
            }
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
            }
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
        catch(Exception exception)
        {
            VenueSync.Log.Debug($"Cancelled downloads failed: {exception.Message}");
        }
    }
}