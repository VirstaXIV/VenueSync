using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VenueSync.State;

namespace VenueSync.Services.Api.Venue;

public sealed class StaffApi
{
    private readonly ApiService _api;

    public StaffApi(Configuration configuration)
    {
        _api = new ApiService(configuration);
    }

    public Task<ApiResult<UserVenueStaffItem>> StoreAsync(string venueId, object payload, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueStaffItem>(
            "venues.staff.store",
            body: payload,
            routeParams: new Dictionary<string, string> { { "venue", venueId } },
            ct: ct
        );
    }

    public Task<ApiResult<UserVenueStaffItem>> UpdateAsync(string venueId, string staffId, object payload, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueStaffItem>(
            "venues.staff.update",
            body: payload,
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "staff", staffId }
            },
            ct: ct
        );
    }

    public Task<ApiResult<UserVenueStaffItem>> DestroyAsync(string venueId, string staffId, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueStaffItem>(
            "venues.staff.destroy",
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "staff", staffId }
            },
            ct: ct
        );
    }
}
