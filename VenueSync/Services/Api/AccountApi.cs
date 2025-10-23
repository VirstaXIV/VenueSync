using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using Newtonsoft.Json;
using OtterGui.Classes;
using VenueSync.Data.DTO.Account;
using VenueSync.State;

namespace VenueSync.Services.Api;

public class AccountApi: IDisposable
{
    private readonly Configuration _configuration;
    private readonly StateService _stateService;
    private readonly ApiService _api;

    public AccountApi(Configuration configuration, StateService stateService)
    {
        _configuration = configuration;
        _stateService = stateService;
        _api = new ApiService(configuration);
    }

    public void Dispose()
    {
        _api.Dispose();
    }

    public async Task<XIVAuthVerification> XIVAuth(CancellationToken cancellationToken = default)
    {
        var sessionID = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var registerResult = await _api.SendAsync<XIVAuthRegisterResponse>(
            "auth.register",
            body: new { session_id = sessionID },
            ct: cancellationToken
        ).ConfigureAwait(false);

        if (!registerResult.Success || registerResult.Data is null ||
            string.IsNullOrWhiteSpace(registerResult.Data.auth_url) ||
            string.IsNullOrWhiteSpace(registerResult.Data.poll_url))
        {
            VenueSync.Messager.NotificationMessage("VenueSync Authentication Failed", NotificationType.Error);
            return new XIVAuthVerification
            {
                Success = false,
                ErrorMessage = registerResult.ErrorMessage ?? "Malformed registration response."
            };
        }

        Util.OpenLink(registerResult.Data.auth_url);
        const int maxAttempts = 600 / 15;
        var pollUri = new Uri(registerResult.Data.poll_url);

        for (int i = 0; i < maxAttempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var poll = await _api.GetAbsoluteAsync<XIVAuthPollResponse>(
                pollUri,
                requiresAuth: false,
                ct: cancellationToken
            ).ConfigureAwait(false);

            if (poll.StatusCode == (int)HttpStatusCode.Gone)
            {
                VenueSync.Messager.NotificationMessage("VenueSync Authentication Failed", NotificationType.Error);
                return new XIVAuthVerification
                {
                    Success = false,
                    ErrorMessage = "Registration session expired. Please try again."
                };
            }

            if (poll.StatusCode == (int)HttpStatusCode.OK && poll.Data is { } lastPoll)
            {
                if (lastPoll.status?.Equals("success", StringComparison.OrdinalIgnoreCase) == true)
                {
                    VenueSync.Messager.NotificationMessage("VenueSync Authentication Successful", NotificationType.Success);
                    return new XIVAuthVerification
                    {
                        Success = true,
                        ErrorMessage = null,
                        UserID = lastPoll.user_id,
                        Token = lastPoll.token
                    };
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        }

        VenueSync.Messager.NotificationMessage("VenueSync Authentication Timed Out", NotificationType.Error);
        return new XIVAuthVerification
        {
            Success = false,
            ErrorMessage = "Timed out waiting for authorisation. Please try again."
        };
    }

    private bool HasAuthentication()
    {
        return !_configuration.ServerToken.IsNullOrEmpty();
    }

    public async Task<UserReply> User(CancellationToken cancellationToken = default)
    {
        if (!HasAuthentication())
        {
            return new UserReply
            {
                Success = false,
                Graceful = true,
                ErrorMessage = "Cannot grab user without token."
            };
        }

        try
        {
            var result = await _api.GetAsync<UserResponse>("me", ct: cancellationToken).ConfigureAwait(false);
            if (!result.Success || result.Data is null)
            {
                VenueSync.Messager.NotificationMessage("VenueSync Request Failed", NotificationType.Error);
                return new UserReply
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage ?? "Malformed user response."
                };
            }

            VenueSync.Log.Debug("Trying to set user state.");

            _stateService.UserState = new UserState
            {
                venues = result.Data.venues,
                characters = result.Data.characters,
                houses = result.Data.houses
            };

            var beforeCount = _stateService.ModsState.modList?.Count ?? 0;
            var incomingCount = result.Data.mods?.Count ?? 0;
            VenueSync.Log.Debug($"ModsState update: before={beforeCount}, incoming={incomingCount}");

            _stateService.ModsState.modList = result.Data.mods ?? [];

            var afterCount = _stateService.ModsState.modList?.Count ?? 0;
            var firstName = afterCount > 0 ? _stateService.ModsState.modList?[0].name : "(none)";
            VenueSync.Log.Debug($"ModsState update complete: after={afterCount}, first='{firstName}'");
        }
        catch (Exception exception)
        {
            VenueSync.Log.Debug($"HTTP Error: {exception.Message}");
            return new UserReply
            {
                Success = false,
                ErrorMessage = "Failed to communicate with service"
            };
        }

        return new UserReply
        {
            Success = true
        };
    }
}