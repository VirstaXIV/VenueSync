using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VenueSync.State;

namespace VenueSync.Services.Api.Venue;

public sealed class ScheduleApi
{
    private readonly ApiService _api;

    public ScheduleApi(Configuration configuration)
    {
        _api = new ApiService(configuration);
    }

    public Task<ApiResult<UserVenueScheduleItem>> StoreAsync(string venueId, object payload, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueScheduleItem>(
            "venues.schedules.store",
            body: payload,
            routeParams: new Dictionary<string, string> { { "venue", venueId } },
            ct: ct
        );
    }

    public Task<ApiResult<UserVenueScheduleItem>> UpdateAsync(string venueId, string scheduleId, object payload, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueScheduleItem>(
            "venues.schedules.update",
            body: payload,
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "schedule", scheduleId }
            },
            ct: ct
        );
    }

    public Task<ApiResult<UserVenueScheduleItem>> DestroyAsync(string venueId, string scheduleId, CancellationToken ct = default)
    {
        return _api.SendAsync<UserVenueScheduleItem>(
            "venues.schedules.destroy",
            routeParams: new Dictionary<string, string>
            {
                { "venue", venueId },
                { "schedule", scheduleId }
            },
            ct: ct
        );
    }
}
