using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using Newtonsoft.Json;
using OtterGui.Classes;
using PusherClient;
using VenueSync.Events;
using VenueSync.Services.Api;
using VenueSync.State;

namespace VenueSync.Services;

class VenueSyncHttpUserAuthenticator : HttpUserAuthenticator
{
    private readonly Configuration _configuration;

    public VenueSyncHttpUserAuthenticator(string authEndpoint, Configuration configuration) 
        : base(authEndpoint)
    {
        _configuration = configuration;
    }

    public override void PreRequest(HttpClient httpClient)
    {
        base.PreRequest(httpClient);
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _configuration.ServerToken);
    }
}

class VenueSyncHttpAuthenticator : HttpAuthorizer
{
    private readonly Configuration _configuration;

    public VenueSyncHttpAuthenticator(string authEndpoint, Configuration configuration) 
        : base(authEndpoint)
    {
        _configuration = configuration;
    }

    public override void PreAuthorize(HttpClient httpClient)
    {
        base.PreAuthorize(httpClient);
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _configuration.ServerToken);
    }
}

public class SocketService: IDisposable
{
    private readonly Configuration _configuration;
    private readonly StateService _stateService;
    private readonly AccountApi _accountApi;
    
    private readonly VenueEntered _venueEntered;
    private readonly VenueExited _venueExited;
    private readonly VenueUpdated _venueUpdated;
    private readonly VenueMod _venueMod;
    private readonly ServiceConnected _serviceConnected;
    private readonly ServiceDisconnected _serviceDisconnected;
    private readonly LoggedIn _loggedIn;
    private readonly LoggedOut _loggedOut;
    
    private Pusher? _pusher;
    private readonly Dictionary<string, Channel> _channels = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    
    private bool _shouldAutoConnect;
    private bool _isManualDisconnect;
    private bool _isDisposed;
    private CancellationTokenSource? _reconnectCts;

    public SocketService(
        Configuration configuration,
        StateService stateService, 
        AccountApi accountApi,
        ServiceConnected serviceConnected, 
        ServiceDisconnected serviceDisconnected, 
        VenueEntered venueEntered, 
        LoggedIn loggedIn, 
        LoggedOut loggedOut,
        VenueUpdated venueUpdated, 
        VenueExited venueExited,
        VenueMod venueMod)
    {
        _configuration = configuration;
        _stateService = stateService;
        _accountApi = accountApi;
        _serviceConnected = serviceConnected;
        _serviceDisconnected = serviceDisconnected;
        _venueEntered = venueEntered;
        _venueUpdated = venueUpdated;
        _venueExited = venueExited;
        _venueMod = venueMod;
        _loggedIn = loggedIn;
        _loggedOut = loggedOut;

        _shouldAutoConnect = _configuration.AutoConnect;

        _loggedIn.Subscribe(OnLoggedIn, LoggedIn.Priority.High);
        _loggedOut.Subscribe(OnLoggedOut, LoggedOut.Priority.High);
        _venueExited.Subscribe(OnVenueExited, VenueExited.Priority.High);
    }

    private void OnLoggedIn()
    {
        if (!_shouldAutoConnect) return;

        VenueSync.Log.Debug("Running auto connect on login");
        _shouldAutoConnect = false;
        
        _ = Task.Run(async () => await ConnectAsync().ConfigureAwait(false));
    }

    private void OnLoggedOut()
    {
        VenueSync.Log.Debug("Logging out, disconnecting from service");
        _ = Task.Run(async () => await DisconnectAsync(true).ConfigureAwait(false));
    }

    private void OnVenueExited(string id)
    {
        VenueSync.Log.Debug($"Leaving venue channel: {id}");
        _ = Task.Run(async () => await RemoveChannelAsync($"presence-venue.{id}").ConfigureAwait(false));
    }
    
    private Pusher CreatePusher()
    {
        var apiEndpoint = Configuration.Constants.API_ENDPOINT;
        var pusher = new Pusher(Configuration.Constants.SOCKET_APP_KEY, new PusherOptions
        {
            UserAuthenticator = new VenueSyncHttpUserAuthenticator($"{apiEndpoint}/broadcasting/user-auth", _configuration),
            Authorizer = new VenueSyncHttpAuthenticator($"{apiEndpoint}/broadcasting/auth", _configuration),
            Host = $"{Configuration.Constants.SOCKET_HOST}:{Configuration.Constants.SOCKET_PORT}",
            Encrypted = Configuration.Constants.SOCKET_SCHEME == "https",
            ClientTimeout = TimeSpan.FromSeconds(20),
        });

        pusher.ConnectionStateChanged += OnConnectionStateChanged;
        pusher.Error += OnPusherError;

        return pusher;
    }
    
    private void OnPusherError(object? sender, PusherException error)
    {
        switch (error)
        {
            case ChannelUnauthorizedException unauthorizedAccess:
                VenueSync.Log.Warning($"Channel authorization failed: {unauthorizedAccess.AuthorizationEndpoint} (403)");
                break;
            case ChannelAuthorizationFailureException httpError:
                VenueSync.Log.Warning($"Authorization endpoint error: {httpError.AuthorizationEndpoint}");
                break;
            case OperationTimeoutException timeoutError:
                VenueSync.Log.Warning($"Connection timed out: {timeoutError.Message}");
                break;
            case ChannelDecryptionException decryptionError:
                VenueSync.Log.Warning($"Decryption error: {decryptionError.Message}");
                break;
            default:
                VenueSync.Log.Warning($"Service error: {error.Message}");
                break;
        }
    }
    
    private async Task AddChannelAsync(string channelName)
    {
        if (_isDisposed)
        {
            VenueSync.Log.Debug($"Cannot add channel {channelName}: service is disposed");
            return;
        }

        var pusher = _pusher;
        if (pusher == null)
        {
            VenueSync.Log.Warning($"Cannot add channel {channelName}: pusher instance is null");
            return;
        }

        await _channelLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_channels.ContainsKey(channelName))
            {
                VenueSync.Log.Debug($"Channel {channelName} already subscribed - skipping");
                return;
            }

            VenueSync.Log.Debug($"Subscribing to channel: {channelName}");
            var channel = await pusher.SubscribeAsync(channelName).ConfigureAwait(false);
            
            if (channelName.StartsWith("private-user."))
            {
                BindUserChannelEvents(channel);
            }
            else if (channelName.StartsWith("presence-venue."))
            {
                BindVenueChannelEvents(channel);
            }
            
            _channels[channelName] = channel;
        
            VenueSync.Log.Debug($"Successfully subscribed to channel: {channelName}");
        }
        catch (Exception ex)
        {
            VenueSync.Log.Warning($"Failed to subscribe to channel {channelName}: {ex.Message}");
        }
        finally
        {
            _channelLock.Release();
        }
    }

    private void BindUserChannelEvents(Channel channel)
    {
        VenueSync.Log.Debug($"Binding user channel events to {channel.Name}");
        
        channel.Unbind("venue.entered", OnVenueEnteredEvent);
        channel.Bind("venue.entered", OnVenueEnteredEvent);
        
        channel.Unbind("venue.mod", OnVenueModEvent);
        channel.Bind("venue.mod", OnVenueModEvent);

        channel.Unbind("character.synced", OnCharacterVerifyEvent);
        channel.Bind("character.synced", OnCharacterVerifyEvent);
    }

    private void BindVenueChannelEvents(Channel channel)
    {
        VenueSync.Log.Debug($"Binding venue channel events to {channel.Name}");
        
        channel.Unbind("venue.updated", OnVenueUpdatedEvent);
        channel.Bind("venue.updated", OnVenueUpdatedEvent);
        
        channel.Unbind("venue.mod", OnVenueModEvent);
        channel.Bind("venue.mod", OnVenueModEvent);
    }

    private void OnVenueEnteredEvent(PusherEvent eventData)
    {
        VenueSync.Log.Debug($"Received event: venue.entered (Channel: {eventData.ChannelName})");

        try
        {
            var enteredData = JsonConvert.DeserializeObject<VenueEnteredData>(eventData.Data);
            if (enteredData != null)
            {
                VenueSync.Log.Debug($"Processing venue.entered for Location ID: {enteredData.location.id}");
                _venueEntered.Invoke(enteredData);
                _ = Task.Run(async () => 
                    await AddChannelAsync($"presence-venue.{enteredData.location.id}").ConfigureAwait(false));
            }
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Error processing venue.entered event: {ex.Message}");
        }
    }
    
    private void OnCharacterVerifyEvent(PusherEvent eventData)
    {
        VenueSync.Log.Debug($"Received event: character.verify (Channel: {eventData.ChannelName})");

        try
        {
            var character = JsonConvert.DeserializeObject<UserCharacterItem>(eventData.Data);
            if (character != null)
            {
                var state = _stateService.UserState;
                if (state?.characters == null)
                {
                    VenueSync.Log.Warning("User state not initialized; cannot update characters.");
                    return;
                }

                var idx = state.characters.FindIndex(c => string.Equals(c.id, character.id, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    state.characters[idx] = character;
                    VenueSync.Log.Debug($"Updated character in state: {character.id}");
                    VenueSync.Messager.NotificationMessage($"Character verified: {character.name}", NotificationType.Success);
                }
                else
                {
                    state.characters.Add(character);
                    VenueSync.Log.Debug($"Added character to state: {character.id}");
                    VenueSync.Messager.NotificationMessage($"Character verified: {character.name}", NotificationType.Success);
                }
            }
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Error processing character.verify event: {ex.Message}");
        }
    }

    private void OnVenueModEvent(PusherEvent eventData)
    {
        VenueSync.Log.Debug($"Received event: venue.mod (Channel: {eventData.ChannelName})");

        try
        {
            var modData = JsonConvert.DeserializeObject<VenueModData>(eventData.Data);
            if (modData != null)
            {
                VenueSync.Log.Debug($"Processing venue.mod for Location ID: {modData.location_id}");
                _venueMod.Invoke(modData);
            }
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Error processing venue.entered event: {ex.Message}");
        }
    }

    private void OnVenueUpdatedEvent(PusherEvent eventData)
    {
        VenueSync.Log.Debug($"Received event: venue.updated (Channel: {eventData.ChannelName})");

        try
        {
            var updatedData = JsonConvert.DeserializeObject<VenueUpdatedData>(eventData.Data);
            if (updatedData != null)
            {
                _venueUpdated.Invoke(updatedData);
            }
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Error processing venue.updated event: {ex.Message}");
        }
    }
    
    private async Task RemoveChannelAsync(string channelName)
    {
        var pusher = _pusher;
        if (pusher == null)
        {
            VenueSync.Log.Debug($"Cannot remove channel {channelName}: pusher instance is null");
            return;
        }

        await _channelLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_channels.TryGetValue(channelName, out var channel))
            {
                channel?.UnbindAll();
                _channels.Remove(channelName);
            }
            else
            {
                VenueSync.Log.Debug($"Channel {channelName} not found in subscribed channels");
            }

            await pusher.UnsubscribeAsync(channelName).ConfigureAwait(false);
            VenueSync.Log.Debug($"Unsubscribed from channel: {channelName}");
        }
        catch (Exception ex)
        {
            VenueSync.Log.Warning($"Failed to unsubscribe from channel {channelName}: {ex.Message}");
        }
        finally
        {
            _channelLock.Release();
        }
    }
    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        var isConnected = state == ConnectionState.Connected;
        var wasConnected = _stateService.Connection.Connected;

        _stateService.Connection.Connected = isConnected;

        if (isConnected && !wasConnected)
        {
            VenueSync.Messager.NotificationMessage("Connected to VenueSync Service.", NotificationType.Success);
        }
        else if (!isConnected && wasConnected)
        {
            VenueSync.Messager.NotificationMessage("Disconnected from VenueSync Service.", NotificationType.Info);

            if (!_isManualDisconnect)
            {
                VenueSync.Log.Debug($"Unexpected disconnection ({state}), initiating reconnect");
                _ = Task.Run(StartReconnectAsync);
            }
        }
    }
    
    public async Task ConnectAsync()
    {
        if (_isDisposed)
        {
            VenueSync.Log.Warning("Cannot connect: service is disposed");
            return;
        }

        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_stateService.Connection.Connected)
            {
                VenueSync.Log.Debug("Already connected");
                return;
            }

            if (!_stateService.Connection.IsLoggedIn)
            {
                VenueSync.Log.Debug("Must be logged in to connect");
                return;
            }

            if (_configuration.ServerToken.IsNullOrEmpty())
            {
                VenueSync.Log.Warning("Cannot connect: server token not configured");
                return;
            }

            _stateService.Connection.Connecting = true;
            _isManualDisconnect = false;

            try
            {
                VenueSync.Log.Debug("Verifying user credentials...");
                var userCheck = await _accountApi.User().ConfigureAwait(false);
                
                if (!userCheck.Success)
                {
                    if (!userCheck.Graceful)
                    {
                        VenueSync.Log.Warning($"Failed to verify user: {userCheck.ErrorMessage}");
                    }
                    return;
                }

                if (_configuration.ServerUserID.IsNullOrEmpty())
                {
                    VenueSync.Log.Warning("Cannot connect: user ID not set");
                    return;
                }

                if (_pusher == null)
                {
                    _pusher = CreatePusher();
                }

                VenueSync.Log.Debug("Connecting to Service...");
                await _pusher.ConnectAsync().ConfigureAwait(false);

                await AddChannelAsync($"private-user.{_configuration.ServerUserID}").ConfigureAwait(false);

                _serviceConnected.Invoke();

                VenueSync.Log.Debug("Successfully connected to service");
            }
            catch (Exception ex)
            {
                VenueSync.Log.Error($"Failed to connect: {ex.Message}");
                await CleanupConnectionAsync().ConfigureAwait(false);
            }
            finally
            {
                _stateService.Connection.Connecting = false;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    public async Task DisconnectAsync(bool manual = false)
    {
        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _isManualDisconnect = manual;
            
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = null;

            if (_pusher == null) return;

            _stateService.Connection.Disconnecting = true;

            try
            {
                VenueSync.Log.Debug("Disconnecting from service...");
                await CleanupConnectionAsync().ConfigureAwait(false);
                VenueSync.Log.Debug("Successfully disconnected");
            }
            catch (Exception ex)
            {
                VenueSync.Log.Warning($"Error during disconnect: {ex.Message}");
            }
            finally
            {
                _stateService.Connection.Connected = false;
                _stateService.Connection.Disconnecting = false;
                _serviceDisconnected.Invoke();
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    
    private async Task CleanupConnectionAsync()
    {
        var pusher = _pusher;
        if (pusher == null) return;

        await _channelLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var channel in _channels.Values.ToList())
            {
                try
                {
                    channel?.UnbindAll();
                }
                catch (Exception ex)
                {
                    VenueSync.Log.Debug($"Error unbinding channel: {ex.Message}");
                }
            }
            _channels.Clear();

            try
            {
                await pusher.UnsubscribeAllAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                VenueSync.Log.Debug($"Error unsubscribing channels: {ex.Message}");
            }

            try
            {
                pusher.UnbindAll();
            }
            catch (Exception ex)
            {
                VenueSync.Log.Debug($"Error unbinding events: {ex.Message}");
            }

            try
            {
                await pusher.DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                VenueSync.Log.Debug($"Error disconnecting: {ex.Message}");
            }

            try
            {
                pusher.ConnectionStateChanged -= OnConnectionStateChanged;
                pusher.Error -= OnPusherError;
            }
            catch (Exception ex)
            {
                VenueSync.Log.Debug($"Error removing event handlers: {ex.Message}");
            }

            _pusher = null;
        }
        catch (Exception ex)
        {
            VenueSync.Log.Warning($"Error during cleanup: {ex.Message}");
        }
        finally
        {
            _channelLock.Release();
        }
    }
    
    private async Task StartReconnectAsync()
    {
        const int maxAttempts = 5;
        const int delaySeconds = 15;

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = new CancellationTokenSource();
        
        var cancellationToken = _reconnectCts.Token;

        try
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (cancellationToken.IsCancellationRequested || _isDisposed)
                {
                    VenueSync.Log.Debug("Reconnection cancelled");
                    return;
                }

                VenueSync.Log.Debug($"Reconnection attempt {attempt}/{maxAttempts}");

                if (await TryReconnectAsync().ConfigureAwait(false))
                {
                    VenueSync.Log.Debug("Reconnection successful");
                    return;
                }

                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken)
                              .ConfigureAwait(false);
                }
            }

            VenueSync.Log.Error("Max reconnection attempts reached. Please reconnect manually.");
        }
        catch (OperationCanceledException)
        {
            VenueSync.Log.Debug("Reconnection cancelled");
        }
        catch (Exception ex)
        {
            VenueSync.Log.Error($"Error during reconnection: {ex.Message}");
        }
    }
    
    private async Task<bool> TryReconnectAsync()
    {
        try
        {
            await CleanupConnectionAsync().ConfigureAwait(false);
            await ConnectAsync().ConfigureAwait(false);
            return _stateService.Connection.Connected;
        }
        catch (Exception ex)
        {
            VenueSync.Log.Warning($"Reconnection failed: {ex.Message}");
            return false;
        }
    }
    
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _loggedIn.Unsubscribe(OnLoggedIn);
        _loggedOut.Unsubscribe(OnLoggedOut);
        _venueExited.Unsubscribe(OnVenueExited);

        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();

        Task.Run(async () =>
        {
            try
            {
                await DisconnectAsync(manual: true).ConfigureAwait(false);
            }
            finally
            {
                _connectionLock.Dispose();
                _channelLock.Dispose();
            }
        }).Wait(TimeSpan.FromSeconds(5));
    }
}
