using System;
using System.Threading;
using System.Threading.Tasks;
using VenueSync.Data.DTO.Characters;

namespace VenueSync.Services.Api;

public class CharacterApi : IDisposable
{
    private readonly Configuration _configuration;
    private readonly ApiService _api;

    public CharacterApi(Configuration configuration)
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
        return !string.IsNullOrWhiteSpace(_configuration.ServerToken);
    }

    public async Task<VerifyCharacterReply> VerifyCharacters(CancellationToken cancellationToken = default)
    {
        if (!HasAuthentication())
        {
            return new VerifyCharacterReply
            {
                Success = false,
                Graceful = true,
                ErrorMessage = "Cannot verify character without token."
            };
        }

        try
        {
            var result = await _api.SendAsync<VerifyCharacterResponse>(
                "characters.verify",
                body: null,
                ct: cancellationToken
            ).ConfigureAwait(false);

            if (!result.Success)
            {
                return new VerifyCharacterReply
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage ?? "Character Verify Request Failed."
                };
            }

            var data = result.Data;
            if (data is null)
            {
                return new VerifyCharacterReply
                {
                    Success = false,
                    ErrorMessage = "Malformed Response"
                };
            }

            if (!data.success)
            {
                return new VerifyCharacterReply
                {
                    Success = false,
                    ErrorMessage = string.IsNullOrWhiteSpace(data.message) ? "Character Verify Request Failed." : data.message
                };
            }
        }
        catch (Exception ex)
        {
            VenueSync.Log.Debug($"HTTP Error verifying character: {ex.Message}");
            return new VerifyCharacterReply
            {
                Success = false,
                ErrorMessage = "Failed to communicate with character verify service."
            };
        }

        return new VerifyCharacterReply
        {
            Success = true
        };
    }
}
