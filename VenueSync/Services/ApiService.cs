using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using VenueSync.Data;

namespace VenueSync.Services;

public sealed class ApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Configuration _configuration;
    private bool _disposed;

    public ApiService(Configuration configuration)
    {
        _configuration = configuration;

        _httpClient = new HttpClient(
            new HttpClientHandler
            {
                AllowAutoRedirect = false,
                MaxAutomaticRedirections = 3
            },
            disposeHandler: false
        );
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }

    private void ApplyCommonHeaders(HttpRequestMessage request, bool requiresAuth)
    {
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("VenueSync",
            $"{ver!.Major}.{ver.Minor}.{ver.Build}"));

        if (requiresAuth && !string.IsNullOrWhiteSpace(_configuration.ServerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ServerToken);
        }
    }

    private static string BuildPath(string pathTemplate, IReadOnlyDictionary<string, string>? routeParams)
    {
        if (routeParams == null || routeParams.Count == 0)
            return pathTemplate;

        var result = pathTemplate;
        foreach (var kv in routeParams)
        {
            result = result.Replace("{" + kv.Key + "}", Uri.EscapeDataString(kv.Value));
        }
        return result;
    }

    private static Uri BuildUri(string baseEndpoint, string path, IReadOnlyDictionary<string, string>? queryParams)
    {
        var baseTrimmed = baseEndpoint.TrimEnd('/');
        var pathTrimmed = path.StartsWith("/") ? path : "/" + path;

        var uriBuilder = new UriBuilder($"{baseTrimmed}{pathTrimmed}");

        if (queryParams is { Count: > 0 })
        {
            var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
            foreach (var kv in queryParams)
                query[kv.Key] = kv.Value;
            uriBuilder.Query = query.ToString();
        }

        return uriBuilder.Uri;
    }

    public async Task<ApiResult<TResponse>> GetAsync<TResponse>(
        string routeKey,
        IReadOnlyDictionary<string, string>? routeParams = null,
        IReadOnlyDictionary<string, string>? queryParams = null,
        CancellationToken ct = default)
    {
        return await SendAsync<TResponse>(routeKey, body: null, routeParams, queryParams, ct).ConfigureAwait(false);
    }

    public async Task<ApiResult<TResponse>> SendAsync<TResponse>(
        string routeKey,
        object? body = null,
        IReadOnlyDictionary<string, string>? routeParams = null,
        IReadOnlyDictionary<string, string>? queryParams = null,
        CancellationToken ct = default)
    {
        var route = ApiRoutes.Get(routeKey);
        var path = BuildPath(route.Path, routeParams);
        var uri = BuildUri(Configuration.Constants.API_ENDPOINT, path, queryParams);

        using var request = new HttpRequestMessage(route.Method, uri);

        if (body is not null && route.Method != HttpMethod.Get && route.Method != HttpMethod.Head)
        {
            request.Content = JsonContent.Create(body);
        }

        ApplyCommonHeaders(request, route.RequiresAuth);

        try
        {
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string? errorBody = null;
                try
                {
                    errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }

                return new ApiResult<TResponse>
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = string.IsNullOrWhiteSpace(errorBody)
                        ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                        : errorBody
                };
            }

            var content = response.Content;
            var hasContent = (content.Headers.ContentLength == null ||
                              content.Headers.ContentLength > 0);

            if (!hasContent)
            {
                return new ApiResult<TResponse>
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Data = default
                };
            }

            try
            {
                var data = await content.ReadFromJsonAsync<TResponse>(cancellationToken: ct).ConfigureAwait(false);
                return new ApiResult<TResponse>
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Data = data
                };
            }
            catch
            {
                return new ApiResult<TResponse>
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Data = default
                };
            }
        }
        catch (OperationCanceledException)
        {
            return new ApiResult<TResponse>
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.RequestTimeout,
                ErrorMessage = "Request cancelled."
            };
        }
        catch (Exception ex)
        {
            return new ApiResult<TResponse>
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.ServiceUnavailable,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<ApiResult<TResponse>> GetAbsoluteAsync<TResponse>(
        Uri uri,
        bool requiresAuth,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        ApplyCommonHeaders(request, requiresAuth);

        try
        {
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string? errorBody = null;
                try
                {
                    errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }

                return new ApiResult<TResponse>
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = string.IsNullOrWhiteSpace(errorBody)
                        ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                        : errorBody
                };
            }

            var content = response.Content;
            var hasContent = (content.Headers.ContentLength == null ||
                              content.Headers.ContentLength > 0);

            if (!hasContent)
            {
                return new ApiResult<TResponse>
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Data = default
                };
            }

            try
            {
                var data = await content.ReadFromJsonAsync<TResponse>(cancellationToken: ct).ConfigureAwait(false);
                return new ApiResult<TResponse>
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Data = data
                };
            }
            catch
            {
                return new ApiResult<TResponse>
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Data = default
                };
            }
        }
        catch (OperationCanceledException)
        {
            return new ApiResult<TResponse>
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.RequestTimeout,
                ErrorMessage = "Request cancelled."
            };
        }
        catch (Exception ex)
        {
            return new ApiResult<TResponse>
            {
                Success = false,
                StatusCode = (int)HttpStatusCode.ServiceUnavailable,
                ErrorMessage = ex.Message
            };
        }
    }
}

public sealed class ApiResult<T>
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public T? Data { get; init; }
}
