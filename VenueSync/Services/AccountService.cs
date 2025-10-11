using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using OtterGui.Classes;
using VenueSync.Data.DTO.Account;
using VenueSync.State;

namespace VenueSync.Services;

public class AccountService: IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Configuration _configuration;
    private readonly StateService _stateService;

    public AccountService(Configuration configuration, StateService stateService)
    {
        _configuration = configuration;
        _stateService = stateService;
        _httpClient = new(
            new HttpClientHandler()
            {
                AllowAutoRedirect = false,
                MaxAutomaticRedirections = 3
            }
        );
    }
    
    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public async Task<XIVAuthVerification> XIVAuth(CancellationToken cancellationToken = default)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VenueSync", ver!.Major + "." + ver!.Minor + "." + ver!.Build));
        
        var sessionID = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        Uri handshakeUri = new Uri(Configuration.Constants.XIVAuthEndpoint);
        var handshakePayload = new {
            session_id = sessionID
        };
        var handshakeResponse = await _httpClient.PostAsJsonAsync(handshakeUri, handshakePayload, cancellationToken).ConfigureAwait(false);
        handshakeResponse.EnsureSuccessStatusCode();
        var register = await handshakeResponse.Content.ReadFromJsonAsync<XIVAuthRegisterResponse>(cancellationToken: cancellationToken)
                                              .ConfigureAwait(false);
        if (register is null || string.IsNullOrWhiteSpace(register.auth_url) ||
            string.IsNullOrWhiteSpace(register.poll_url))
        {
            VenueSync.Messager.NotificationMessage($"VenueSync Authentication Failed", NotificationType.Error);
            return new XIVAuthVerification() {
                Success = false,
                ErrorMessage = "Malformed registration response."
            };
        }

        Util.OpenLink(register.auth_url);
        const int maxAttempts = 600 / 15;
        var pollUri = new Uri(register.poll_url);
        for (int i = 0; i < maxAttempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var resp = await _httpClient.GetAsync(pollUri, cancellationToken).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.Gone)
            {
                VenueSync.Messager.NotificationMessage($"VenueSync Authentication Failed", NotificationType.Error);
                return new XIVAuthVerification() {
                    Success = false,
                    ErrorMessage = "Registration session expired. Please try again."
                };
            }

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var lastPoll = await resp.Content.ReadFromJsonAsync<XIVAuthPollResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

                if (lastPoll?.status?.Equals("success", StringComparison.OrdinalIgnoreCase) == true)
                {
                    VenueSync.Messager.NotificationMessage($"VenueSync Authentication Successful", NotificationType.Success);
                    return new XIVAuthVerification() {
                        Success = true,
                        ErrorMessage = null,
                        UserID = lastPoll.user_id,
                        Token = lastPoll.token
                    };
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        }
        
        VenueSync.Messager.NotificationMessage($"VenueSync Authentication Timed Out", NotificationType.Error);
        return new XIVAuthVerification() {
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
            return new UserReply() {
                Success = false,
                Graceful = true,
                ErrorMessage = "Cannot grab user without token."
            };
        }
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ServerToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VenueSync", ver!.Major + "." + ver!.Minor + "." + ver!.Build));
        
        Uri endpointUri = new Uri(Configuration.Constants.MeEndpoint);
        var response = await _httpClient.GetAsync(endpointUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken: cancellationToken)
                               .ConfigureAwait(false);
        if (me is null)
        {
            VenueSync.Messager.NotificationMessage($"VenueSync Request Failed", NotificationType.Error);
            return new UserReply() {
                Success = false,
                ErrorMessage = "Malformed user response."
            };
        }
        
        VenueSync.Log.Debug($"Trying to set user state.");

        _stateService.UserState = new UserState() {
            venues = me.venues,
            characters = me.characters,
            houses = me.houses
        };

        return new UserReply() {
            Success = true
        };
    }
}