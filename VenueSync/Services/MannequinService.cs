using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;
using VenueSync.Data.DTO.Mannequins;
using VenueSync.State;

namespace VenueSync.Services;

public class MannequinService: IDisposable
{
    private readonly Configuration _configuration;
    private readonly ApiService _api;

    public MannequinService(Configuration configuration)
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

    private static string FormatWorldName(string worldName)
    {
        return worldName.ToLower();
    }

    private static string FormatDataCenter(string dataCenter)
    {
        return dataCenter.ToLower();
    }

    public async Task<UpdateMannequinReply> UpdateMannequin(Mannequin mannequinData, CancellationToken cancellationToken = default)
    {
        if (!HasAuthentication())
        {
            return new UpdateMannequinReply
            {
                Success = false,
                Graceful = true,
                ErrorMessage = "Cannot grab mannequin without token."
            };
        }

        var mannequinPayload = new
        {
            ffxiv_id = mannequinData.ffxiv_id,
            name = mannequinData.name,
            world = FormatWorldName(mannequinData.world),
            data_center = FormatDataCenter(mannequinData.data_center),
        };

        try
        {
            var result = await _api.SendAsync<UpdateMannequinResponse>(
                "mannequin.update",
                body: mannequinPayload,
                ct: cancellationToken
            ).ConfigureAwait(false);

            if (!result.Success)
            {
                return new UpdateMannequinReply
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage ?? "Mannequin Request Failed."
                };
            }

            var mannequin = result.Data;
            if (mannequin is null)
            {
                return new UpdateMannequinReply
                {
                    Success = false,
                    ErrorMessage = "Malformed Response"
                };
            }

            if (!mannequin.success)
            {
                return new UpdateMannequinReply
                {
                    Success = false,
                    ErrorMessage = "Mannequin Request Failed."
                };
            }
        }
        catch (Exception exception)
        {
            VenueSync.Log.Debug($"HTTP Error: {exception.Message}");
            return new UpdateMannequinReply
            {
                Success = false,
                ErrorMessage = "Failed to communicate with mannequin service."
            };
        }

        return new UpdateMannequinReply
        {
            Success = true
        };
    }
}