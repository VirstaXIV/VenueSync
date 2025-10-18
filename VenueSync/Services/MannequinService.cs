using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Utility;
using VenueSync.Data.DTO.Mannequins;
using VenueSync.State;

namespace VenueSync.Services;

public class MannequinService: IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Configuration _configuration;

    public MannequinService(Configuration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient(
            new HttpClientHandler()
            {
                AllowAutoRedirect = false,
                MaxAutomaticRedirections = 3
            },
            disposeHandler: false
        );
        
        SetUserAgent();
    }
    
    public void Dispose()
    {
        _httpClient.Dispose();
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

    private void SetUserAgent()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("VenueSync", $"{ver!.Major}.{ver.Minor}.{ver.Build}")
        );
    }

    private void ConfigureRequestHeaders()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ServerToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        SetUserAgent();
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

        ConfigureRequestHeaders();

        var mannequinUri = new Uri(Configuration.Constants.MannequinEndpoint);
        var mannequinPayload = new
        {
            ffxiv_id = mannequinData.ffxiv_id,
            name = mannequinData.name,
            world = FormatWorldName(mannequinData.world),
            data_center = FormatDataCenter(mannequinData.data_center),
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(mannequinUri, mannequinPayload, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var mannequin = await response.Content.ReadFromJsonAsync<UpdateMannequinResponse>(cancellationToken: cancellationToken)
                                          .ConfigureAwait(false);

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