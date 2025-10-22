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

    public Task<ApiResult<UserVenueLocationModItem>> AddModAsync(string venueId, string locationId, string modId, string mannequinId, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            { "mod_id", modId },
            { "mannequin_id", mannequinId },
        };

        return _api.SendAsync<UserVenueLocationModItem>(
            "venues.locations.mods.store",
            body: payload,
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "location", locationId }
            },
            ct: ct
        );
    }

    public Task<ApiResult<UserVenueLocationModItem>> ToggleModAsync(string venueId, string locationId, string locationModId, CancellationToken ct = default)
    {
        // Sends no body, server toggles enabled state.
        return _api.SendAsync<UserVenueLocationModItem>(
            "venues.locations.mods.update",
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "location", locationId },
                { "mod", locationModId }
            },
            ct: ct
        );
    }

    public Task<ApiResult<object>> DeleteModAsync(string venueId, string locationId, string locationModId, CancellationToken ct = default)
    {
        return _api.SendAsync<object>(
            "venues.locations.mods.destroy",
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "location", locationId },
                { "mod", locationModId }
            },
            ct: ct
        );
    }
}
