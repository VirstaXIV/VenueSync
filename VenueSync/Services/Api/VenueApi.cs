using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using VenueSync.State;

namespace VenueSync.Services.Api;

public class VenueApi : IDisposable
{
    private readonly ApiService _api;
    private bool _disposed;

    public VenueApi(Configuration configuration)
    {
        _api = new ApiService(configuration);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _api.Dispose();
    }

    // POST /venues
    public Task<ApiResult<UserVenueItem>> StoreAsync(object venuePayload, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueItem>(
            "venues.store",
            body: venuePayload,
            ct: ct
        );
    }

    // POST /venues/{venue}
    public Task<ApiResult<UserVenueItem>> UpdateAsync(string venueId, object venuePayload, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueItem>(
            "venues.update",
            body: venuePayload,
            routeParams: new Dictionary<string, string> { { "venue", venueId } },
            ct: ct
        );
    }

    // POST /venues/{venue}/logo
    public Task<ApiResult<object>> UploadLogoAsync(
        string venueId,
        Stream logoStream,
        string fileName,
        string contentType = "application/octet-stream",
        CancellationToken ct = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new StreamContent(logoStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "logo", fileName);

        return _api.SendAsync<object>(
            "venues.logo",
            body: multipart,
            routeParams: new Dictionary<string, string> { { "venue", venueId } },
            ct: ct
        );
    }

    // DELETE /venues/{venue}
    public Task<ApiResult<object>> DestroyAsync(string venueId, CancellationToken ct = default)
    {
        return _api.SendAsync<object>(
            "venues.destroy",
            routeParams: new Dictionary<string, string> { { "venue", venueId } },
            ct: ct
        );
    }
}
