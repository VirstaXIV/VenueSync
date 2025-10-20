using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;
using VenueSync.Data.DTO.Locations;
using VenueSync.State;

namespace VenueSync.Services.Api;

public class LocationApi: IDisposable
{
    private readonly Configuration _configuration;
    private readonly ApiService _api;

    public LocationApi(Configuration configuration)
    {
        _configuration = configuration;
        _api = new ApiService(configuration);
    }

    public void Dispose()
    {
        _api.Dispose();
    }

    private bool HasAuthentication()
    {
        return !_configuration.ServerToken.IsNullOrEmpty();
    }

    private string FormatWorldName(string worldName)
    {
        return worldName.ToLower();
    }

    private string FormatDataCenter(string dataCenter)
    {
        return dataCenter.ToLower();
    }

    public async Task<SendLocationReply> SendLocation(House house, CancellationToken cancellationToken = default)
    {
        if (!HasAuthentication())
        {
            return new SendLocationReply
            {
                Success = false,
                Graceful = true,
                ErrorMessage = "Cannot grab location without token."
            };
        }

        var locationPayload = new
        {
            district = house.District,
            plot = house.Plot,
            ward = house.Ward,
            room = house.Room,
            world_id = house.WorldId,
            size = house.Type,
            world = FormatWorldName(house.WorldName),
            data_center = FormatDataCenter(house.DataCenter),
            ffxiv_id = house.HouseId
        };

        try
        {
            var result = await _api.SendAsync<SendLocationResponse>(
                "location.send",
                body: locationPayload,
                ct: cancellationToken
            ).ConfigureAwait(false);

            if (!result.Success)
            {
                return new SendLocationReply
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage ?? "Location Request Failed."
                };
            }

            var location = result.Data;
            if (location is null)
            {
                return new SendLocationReply
                {
                    Success = false,
                    ErrorMessage = "Malformed Response"
                };
            }

            if (!location.success)
            {
                return new SendLocationReply
                {
                    Success = false,
                    ErrorMessage = "Location Request Failed."
                };
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Debug($"HTTP Error: {exception.Message}");
            return new SendLocationReply
            {
                Success = false,
                ErrorMessage = "Failed to communicate with location service."
            };
        }

        return new SendLocationReply
        {
            Success = true
        };
    }

    public async Task<VerifyLocationReply> VerifyLocation(string characterName, string lodestoneId, House house, CancellationToken cancellationToken = default)
    {
        if (!HasAuthentication())
        {
            return new VerifyLocationReply
            {
                Success = false,
                Graceful = true,
                ErrorMessage = "Cannot verify location without token."
            };
        }

        var locationPayload = new
        {
            character_name = characterName,
            lodestone_id = lodestoneId,
            district = house.District,
            plot = house.Plot,
            ward = house.Ward,
            size = house.Type,
            world = FormatWorldName(house.WorldName),
            data_center = FormatDataCenter(house.DataCenter),
            ffxiv_id = house.HouseId
        };

        try
        {
            var result = await _api.SendAsync<VerifyLocationResponse>(
                "location.verify",
                body: locationPayload,
                ct: cancellationToken
            ).ConfigureAwait(false);

            if (!result.Success)
            {
                return new VerifyLocationReply
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage ?? "Verify Request Failed."
                };
            }

            var location = result.Data;
            if (location is null)
            {
                return new VerifyLocationReply
                {
                    Success = false,
                    ErrorMessage = "Malformed Response"
                };
            }

            if (!location.success)
            {
                return new VerifyLocationReply
                {
                    Success = false,
                    ErrorMessage = location.message
                };
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Debug($"HTTP Error: {exception.Message}");
            return new VerifyLocationReply
            {
                Success = false,
                ErrorMessage = "Failed to communicate with verify service."
            };
        }

        return new VerifyLocationReply
        {
            Success = true
        };
    }

    public async Task<bool> SendActiveStream(string venueId, string locationId, string activeStream, CancellationToken cancellationToken = default)
    {
        if (!HasAuthentication())
        {
            VenueSync.Log.Debug("Cannot send active stream without token.");
            return false;
        }

        var streamPayload = new
        {
            venue_id = venueId,
            location_id = locationId,
            active_stream = activeStream
        };

        try
        {
            var result = await _api.SendAsync<object?>(
                "location.activeStream",
                body: streamPayload,
                ct: cancellationToken
            ).ConfigureAwait(false);

            return result.Success;
        }
        catch (Exception exception)
        {
            VenueSync.Log.Debug($"HTTP Error sending active stream: {exception.Message}");
            return false;
        }
    }
}
