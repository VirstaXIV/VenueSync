using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VenueSync.State;

namespace VenueSync.Services.Api;

public sealed class HouseApi
{
    private readonly ApiService _api;

    public HouseApi(Configuration configuration)
    {
        _api = new ApiService(configuration);
    }

    public Task<ApiResult<UserHouseGrantItem>> StoreGrantAsync(string houseId, string lodestoneId, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            { "lodestone_id", lodestoneId }
        };

        return _api.SendAsync<UserHouseGrantItem>(
            "houses.grant.store",
            body: payload,
            routeParams: new Dictionary<string, string>
            {
                { "house", houseId }
            },
            ct: ct
        );
    }

    public Task<ApiResult<object>> DestroyGrantAsync(string houseId, string grantId, CancellationToken ct = default)
    {
        return _api.SendAsync<object>(
            "houses.grant.destroy",
            routeParams: new Dictionary<string, string>
            {
                { "house", houseId },
                { "houseGrant", grantId }
            },
            ct: ct
        );
    }
}
