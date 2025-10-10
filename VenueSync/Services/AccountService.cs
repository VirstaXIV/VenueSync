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

namespace VenueSync.Services;

public class AccountService: IDisposable
{
    private readonly HttpClient _httpClient;

    public AccountService()
    {
        _httpClient = new(
            new HttpClientHandler()
            {
                AllowAutoRedirect = false,
                MaxAutomaticRedirections = 3
            }
        );
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VenueSync", ver!.Major + "." + ver!.Minor + "." + ver!.Build));
    }
    
    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public async Task<XIVAuthReply> XIVAuth(CancellationToken cancellationToken = default)
    {
        var sessionID = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        Uri handshakeUri = new Uri(Configuration.Constants.XIVAuthEndpoint);
        var handshakePayload = new {
            session_id = sessionID
        };
        var handshakeResponse = await _httpClient.PostAsJsonAsync(handshakeUri, handshakePayload, cancellationToken).ConfigureAwait(false);
        handshakeResponse.EnsureSuccessStatusCode();
        var register = await handshakeResponse.Content.ReadFromJsonAsync<RegisterResponse>(cancellationToken: cancellationToken)
                                              .ConfigureAwait(false);
        if (register is null || string.IsNullOrWhiteSpace(register.auth_url) ||
            string.IsNullOrWhiteSpace(register.poll_url))
        {
            VenueSync.Messager.NotificationMessage($"VenueSync Authentication Failed", NotificationType.Error);
            return new XIVAuthReply() {
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
                return new XIVAuthReply() {
                    Success = false,
                    ErrorMessage = "Registration session expired. Please try again."
                };
            }

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var lastPoll = await resp.Content.ReadFromJsonAsync<PollResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

                if (lastPoll?.status?.Equals("success", StringComparison.OrdinalIgnoreCase) == true)
                {
                    VenueSync.Messager.NotificationMessage($"VenueSync Authentication Successful", NotificationType.Success);
                    return new XIVAuthReply() {
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
        return new XIVAuthReply() {
            Success = false,
            ErrorMessage = "Timed out waiting for authorisation. Please try again."
        };
    }

    private sealed class RegisterResponse
    {
        public string auth_url { get; set; } = "";
        public string poll_url { get; set; } = "";
    }
    
    private sealed class PollResponse
    {
        public string status { get; set; } = "";
        public string? user_id { get; set; } = "";
        public string? token { get; set; }
        
    }
    
    public record XIVAuthReply
    {
        public bool Success { get; set; } = false;
        public string? ErrorMessage { get; set; } = string.Empty;
        public string? UserID { get; set; } = string.Empty;
        public string? Token { get; set; } = string.Empty;
    }
}