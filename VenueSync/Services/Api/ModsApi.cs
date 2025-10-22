using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VenueSync.State;

namespace VenueSync.Services.Api;

public class ModsApi : IDisposable
{
    private readonly ApiService _api;

    public ModsApi(Configuration configuration)
    {
        _api = new ApiService(configuration);
    }

    public void Dispose()
    {
        _api.Dispose();
    }

    public async Task<ApiResult<ModItem>> SaveMod(ModItem mod, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                name = mod.name,
                is_public = mod.is_public,
                version_id = mod.version_id
            };

            var routeKey = string.IsNullOrWhiteSpace(mod.id) ? "mods.store" : "mods.update";
            var routeParams = new Dictionary<string, string>();
            if (routeKey == "mods.update")
            {
                routeParams.Add("mod", mod.id);
            }

            return await _api.SendAsync<ModItem>(
                routeKey,
                body: payload,
                routeParams: routeParams,
                ct: ct
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"SaveMod error: {ex.Message}");
            return new ApiResult<ModItem>
            {
                Success = false,
                ErrorMessage = "Exception saving mod",
                Data = null
            };
        }
    }

    public async Task<ApiResult<object>> DeleteMod(string modId, CancellationToken ct = default)
    {
        try
        {
            return await _api.SendAsync<object>(
                "mods.destroy",
                routeParams: new Dictionary<string, string> { { "mod", modId } },
                ct: ct
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"DeleteMod error: {ex.Message}");
            return new ApiResult<object>
            {
                Success = false,
                ErrorMessage = "Exception deleting mod",
                Data = null
            };
        }
    }

    public async Task<ApiResult<ModItem>> UploadVersion(string modId, string pmpFilePath, CancellationToken ct = default)
    {
        try
        {
            var content = new MultipartFormDataContent();
            var fileBytes = await File.ReadAllBytesAsync(pmpFilePath, ct).ConfigureAwait(false);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(pmpFilePath));

            return await _api.SendMultipartAsync<ModItem>(
                "mods.versions.store",
                multipartContent: content,
                routeParams: new Dictionary<string, string> { { "mod", modId } },
                ct: ct
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"UploadVersion error: {ex.Message}");
            return new ApiResult<ModItem>
            {
                Success = false,
                ErrorMessage = "Exception uploading version",
                Data = null
            };
        }
    }

    public async Task<ApiResult<ModItem>> DeleteVersion(string modId, string versionId, CancellationToken ct = default)
    {
        try
        {
            return await _api.SendAsync<ModItem>(
                "mods.versions.destroy",
                routeParams: new Dictionary<string, string> { { "mod", modId }, { "version", versionId } },
                ct: ct
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"DeleteVersion error: {ex.Message}");
            return new ApiResult<ModItem>
            {
                Success = false,
                ErrorMessage = "Exception deleting version",
                Data = null
            };
        }
    }
}
