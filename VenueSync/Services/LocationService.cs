using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;

namespace VenueSync.Services;

public class LocationService: IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Configuration _configuration;

    public LocationService(Configuration configuration)
    {
        _configuration = configuration;
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
            return new SendLocationReply() {
                Success = false,
                Graceful = true,
                ErrorMessage = "Cannot grab location without token."
            };
        }
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ServerToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        Uri handshakeUri = new Uri(Configuration.Constants.LocationEndpoint);
        var handshakePayload = new {
            district = house.District,
            plot = house.Plot,
            ward = house.Ward,
            room = house.Room,
            world_id = house.WorldId,
            size = house.Type,
            world = FormatWorldName(house.WorldName),
            data_center = FormatDataCenter(house.DataCenter),
            house_id = house.HouseId
        };
        var handshakeResponse = await _httpClient.PostAsJsonAsync(handshakeUri, handshakePayload, cancellationToken).ConfigureAwait(false);
        handshakeResponse.EnsureSuccessStatusCode();
        var location = await handshakeResponse.Content.ReadFromJsonAsync<SendLocationResponse>(cancellationToken: cancellationToken)
                                              .ConfigureAwait(false);
        if (location is null)
        {
            return new SendLocationReply() {
                Success = false,
                ErrorMessage = "Malformed Response"
            };
        }

        if (!location.success)
        {
            return new SendLocationReply() {
                Success = false,
                ErrorMessage = "Location Request Failed."
            };
        }

        return new SendLocationReply() {
            Success = true
        };
    }
    
    private sealed class SendLocationResponse
    {
        public bool success { get; set; } = false;
    }
    
    public record SendLocationReply
    {
        public bool Success { get; set; } = false;
        public bool Graceful { get; set; } = false;
        public string? ErrorMessage { get; set; } = string.Empty;
    }
}
