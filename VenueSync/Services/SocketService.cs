using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using Newtonsoft.Json;
using OtterGui.Classes;
using PusherClient;
using VenueSync.Events;

namespace VenueSync.Services;

class VenueSyncHttpUserAuthenticator(string authEndpoint, Configuration configuration) : HttpUserAuthenticator(authEndpoint)
{
    public override void PreRequest(HttpClient httpClient)
    {
        base.PreRequest(httpClient);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ServerToken);
    }
}

class VenueSyncHttpAuthenticator(string authEndpoint, Configuration configuration) : HttpAuthorizer(authEndpoint)
{
    public override void PreAuthorize(HttpClient httpClient)
    {
        base.PreAuthorize(httpClient);

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ServerToken);
    }
}

public class SocketService: IDisposable
{
    private readonly Configuration _configuration;
    private readonly VenueEntered _venueEntered;
    private Pusher? pusher { get; set; } = null;
    private Dictionary<string, Channel> _channels = new Dictionary<string, Channel>();
    
    public bool Connected = false;
    public bool WasConnected = false;
    public bool Loaded = false;

    public string UserID = string.Empty;
    
    public SocketService(Configuration configuration, VenueEntered @venueEntered)
    {
        _configuration = configuration;
        _venueEntered = @venueEntered;
    }

    private Pusher GetPusher()
    {
        if (Loaded && pusher?.GetType() == typeof(Pusher))
        {
            return pusher;
        }
        
        return Load();
    }

    private Pusher Load()
    {
        pusher = new Pusher(Configuration.Constants.SOCKET_APP_KEY, new PusherOptions
        {
            UserAuthenticator = new VenueSyncHttpUserAuthenticator(Configuration.Constants.SOCKET_USER_AUTH, _configuration),
            Authorizer = new VenueSyncHttpAuthenticator(Configuration.Constants.SOCKET_CHANNEL_AUTH, _configuration),
            Host = Configuration.Constants.SOCKET_HOST + ":" + Configuration.Constants.SOCKET_PORT,
            Encrypted = Configuration.Constants.SOCKET_SCHEME == "https",
            ClientTimeout = TimeSpan.FromSeconds(20),
        });
        pusher.ConnectionStateChanged += ConnectionStateChanged;
        pusher.Error += HandleError;
        
        Loaded = true;
        
        return pusher;
    }
    
    void HandleError(object sender, PusherException error)
    {
        if (error is ChannelUnauthorizedException unauthorizedAccess)
        {
            // Private and Presence channel failed authorization with Forbidden (403)
            VenueSync.Log.Warning($"Could not enter channel: {unauthorizedAccess.AuthorizationEndpoint} (403)");
        }
        else if (error is ChannelAuthorizationFailureException httpError)
        {
            // Authorization endpoint returned an HTTP error other than Forbidden (403)
            VenueSync.Log.Warning($"Could not authorize: {httpError.AuthorizationEndpoint} (403)");
        }
        else if (error is OperationTimeoutException timeoutError)
        {
            // A client operation has timed-out. Governed by PusherOptions.ClientTimeout
            VenueSync.Log.Warning($"Connection Timed out: {timeoutError.Message}");
        }
        else if (error is ChannelDecryptionException decryptionError)
        {
            // Failed to decrypt the data for a private encrypted channel
            VenueSync.Log.Warning($"Decrypt Error: {decryptionError.Message}");
        }
        else
        {
            VenueSync.Log.Warning($"Unknown Error: {error.Message}");
        }
    }

    public async Task AddChannel(string channelName)
    {
        try
        {
            Channel channel = await GetPusher().SubscribeAsync(channelName).ConfigureAwait(false);
            channel.BindAll(Listener);
            _channels.Add(channelName, channel);
            
            VenueSync.Log.Information($"Added channel: {channelName}");
        }
        catch (ChannelUnauthorizedException)
        {
            VenueSync.Log.Warning($"Could not enter channel: {channelName}");
        }
        catch (Exception)
        {
            VenueSync.Log.Warning($"Could not enter channel: {channelName}");
        }
    }

    public async Task RemoveChannel(string channelName)
    {
        try
        {
            var channel = _channels.GetValueOrDefault(channelName);
            channel?.UnbindAll();
            await GetPusher().UnsubscribeAsync(channelName).ConfigureAwait(false);
            _channels.Remove(channelName);
            
            VenueSync.Log.Information($"Left channel: {channelName}");
        }
        catch (ChannelUnauthorizedException)
        {
            VenueSync.Log.Warning($"Could not leave channel: {channelName}");
        }
        catch (Exception)
        {
            VenueSync.Log.Warning($"Could not leave channel: {channelName}");
        }
    }

    private void ConnectionStateChanged(object sender, ConnectionState state)
    {
        Connected = state switch {
            ConnectionState.Connected => true,
            ConnectionState.Disconnected => false,
            _ => Connected
        };

        if (Connected && !WasConnected)
        {
            WasConnected = true;
            VenueSync.Messager.NotificationMessage($"Connected to VenueSync Service.", NotificationType.Success);
        }
        else
        {
            if (!WasConnected)
            {
                return;
            }
            WasConnected = false;
            VenueSync.Messager.NotificationMessage($"Disconnected from VenueSync Service.", NotificationType.Info);
        }
    }

    private bool CheckToken()
    {
        return !_configuration.ServerToken.IsNullOrEmpty();
    }

    private string GetUserID()
    {
        return _configuration.ServerUserID;
    }

    private void Listener(string eventName, PusherEvent eventData)
    {
        if (eventName == "venue.entered")
        {
            var data = JsonConvert.DeserializeObject<VenueEnteredData>(eventData.Data);
            if (data != null)
            {
                _venueEntered.Invoke(data);
            }
        }
        VenueSync.Log.Information($"Got event: {eventName}");
    }

    public async Task Connect()
    {
        if (CheckToken())
        {
            try
            {
                VenueSync.Log.Information($"Trying to connect to service.");
                await GetPusher().ConnectAsync().ConfigureAwait(false);

                var userId = GetUserID();

                if (!userId.IsNullOrEmpty())
                {
                    await AddChannel($"private-user.{userId}");
                }
                else
                {
                    VenueSync.Log.Warning($"No User ID was found.");
                }
            }
            catch (Exception ex)
            {
                VenueSync.Log.Warning($"Could not connect to socket: {ex.Message}");
            }
        }
        else
        {
            VenueSync.Log.Warning($"Tried to connect without token.");
        }
    }

    public async Task Disconnect()
    {
        try
        {
            VenueSync.Log.Information($"Trying to disconnect from service.");
            GetPusher().UnbindAll();
            await GetPusher().UnsubscribeAllAsync().ConfigureAwait(false);
            _channels.Clear();
            await GetPusher().DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            VenueSync.Log.Warning($"Could not disconnect from socket.");
        }
    }

    public void Dispose()
    {
        Task.Run(async () =>
        {
            await Disconnect();
            GetPusher().ConnectionStateChanged -= ConnectionStateChanged;
            GetPusher().Error -= HandleError;
        });
    }
}
