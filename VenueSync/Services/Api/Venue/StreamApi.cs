using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using VenueSync.State;

namespace VenueSync.Services.Api.Venue;

public sealed class StreamApi
{
    private readonly ApiService _api;

    public StreamApi(Configuration configuration)
    {
        _api = new ApiService(configuration);
    }

    public Task<ApiResult<UserVenueStreamItem>> StoreAsync(string venueId, object payload, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueStreamItem>(
            "venues.streams.store",
            body: payload,
            routeParams: new Dictionary<string, string> { { "venue", venueId } },
            ct: ct
        );
    }

    public Task<ApiResult<UserVenueStreamItem>> UpdateAsync(string venueId, string streamId, object payload, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueStreamItem>(
            "venues.streams.update",
            body: payload,
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "stream", streamId }
            },
            ct: ct
        );
    }

    public Task<ApiResult<UserVenueStreamItem>> DestroyAsync(string venueId, string streamId, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueStreamItem>(
            "venues.streams.destroy",
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "stream", streamId }
            },
            ct: ct
        );
    }

    // POST /venues/{venue}/streams/{stream}/logo
    public Task<ApiResult<UserVenueStreamItem>> UploadLogoAsync(
        string venueId,
        string streamId,
        Stream logoStream,
        string fileName,
        string contentType = "application/octet-stream",
        CancellationToken ct = default)
    {
        var multipart = new MultipartFormDataContent();
        var fileContent = new StreamContent(logoStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "logo", fileName);

        return _api.SendAsync<UserVenueStreamItem>(
            "venues.streams.logo",
            body: multipart,
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "stream", streamId }
            },
            ct: ct
        );
    }
}
