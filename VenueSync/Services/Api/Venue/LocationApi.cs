using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VenueSync.State;

namespace VenueSync.Services.Api.Venue;

public sealed class LocationApi
{
    private readonly ApiService _api;

    public LocationApi(Configuration configuration)
    {
        _api = new ApiService(configuration);
    }

    public Task<ApiResult<UserVenueLocationItem>> StoreAsync(string venueId, object payload, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueLocationItem>(
            "venues.locations.store",
            body: payload,
            routeParams: new Dictionary<string, string> { { "venue", venueId } },
            ct: ct
        );
    }

    public Task<ApiResult<UserVenueLocationItem>> UpdateAsync(string venueId, string locationId, object payload, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueLocationItem>(
            "venues.locations.update",
            body: payload,
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "location", locationId }
            },
            ct: ct
        );
    }

    public Task<ApiResult<UserVenueLocationItem>> DestroyAsync(string venueId, string locationId, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueLocationItem>(
            "venues.locations.destroy",
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "location", locationId }
            },
            ct: ct
        );
    }
}
