using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;
using VenueSync.Data.DTO.Locations;
using VenueSync.State;

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
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ServerToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VenueSync", ver!.Major + "." + ver!.Minor + "." + ver!.Build));

        Uri locationUri = new Uri(Configuration.Constants.LocationEndpoint);
        var locationPayload = new {
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
        var response = await _httpClient.PostAsJsonAsync(locationUri, locationPayload, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var location = await response.Content.ReadFromJsonAsync<SendLocationResponse>(cancellationToken: cancellationToken)
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
}
