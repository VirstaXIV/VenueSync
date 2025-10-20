using System.Collections.Generic;
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
}
