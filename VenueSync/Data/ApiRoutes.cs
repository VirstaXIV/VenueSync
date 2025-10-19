using System;
using System.Collections.Generic;
using System.Net.Http;

namespace VenueSync.Data;

public static class ApiRoutes
{
    public sealed record ApiRoute(string Key, HttpMethod Method, string Path, bool RequiresAuth);

    private static readonly Dictionary<string, ApiRoute> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Authentication
        { "auth.register", new ApiRoute("auth.register", HttpMethod.Post, "/auth/xivauth/register", false) },

        // User
        { "me", new ApiRoute("me", HttpMethod.Get, "/me", true) },

        // Locations
        { "location.send", new ApiRoute("location.send", HttpMethod.Post, "/location/send", true) },
        { "location.verify", new ApiRoute("location.verify", HttpMethod.Post, "/location/verify", true) },
        { "location.activeStream", new ApiRoute("location.activeStream", HttpMethod.Post, "/location/active-stream", true) },

        // Mannequin
        { "mannequin.update", new ApiRoute("mannequin.update", HttpMethod.Post, "/mannequin/update", true) },
    };

    public static ApiRoute Get(string key)
    {
        if (!Routes.TryGetValue(key, out var route))
            throw new KeyNotFoundException($"API route '{key}' is not defined.");
        return route;
    }
}
