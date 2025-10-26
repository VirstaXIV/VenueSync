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

        // Characters
        { "characters.verify", new ApiRoute("characters.verify", HttpMethod.Post, "/characters/verify", true) },

        // Venues
        { "venues.store", new ApiRoute("venues.store", HttpMethod.Post, "/venues", true) },
        { "venues.update", new ApiRoute("venues.update", HttpMethod.Post, "/venues/{venue}", true) },
        { "venues.logo", new ApiRoute("venues.logo", HttpMethod.Post, "/venues/{venue}/logo", true) },
        { "venues.destroy", new ApiRoute("venues.destroy", HttpMethod.Delete, "/venues/{venue}", true) },
        
        // Streams
        { "venues.locations.store", new ApiRoute("venues.locations.store", HttpMethod.Post, "/venues/{venue}/locations", true) },
        { "venues.locations.update", new ApiRoute("venues.locations.update", HttpMethod.Post, "/venues/{venue}/locations/{location}", true) },
        { "venues.locations.destroy", new ApiRoute("venues.locations.destroy", HttpMethod.Delete, "/venues/{venue}/locations/{location}", true) },
        
        // Staff
        { "venues.staff.store", new ApiRoute("venues.staff.store", HttpMethod.Post, "/venues/{venue}/members", true) },
        { "venues.staff.update", new ApiRoute("venues.staff.update", HttpMethod.Post, "/venues/{venue}/members/{staff}", true) },
        { "venues.staff.destroy", new ApiRoute("venues.staff.destroy", HttpMethod.Delete, "/venues/{venue}/members/{staff}", true) },
        
        // Schedules
        { "venues.schedules.store", new ApiRoute("venues.schedules.store", HttpMethod.Post, "/venues/{venue}/schedules", true) },
        { "venues.schedules.update", new ApiRoute("venues.schedules.update", HttpMethod.Post, "/venues/{venue}/schedules/{schedule}", true) },
        { "venues.schedules.destroy", new ApiRoute("venues.schedules.destroy", HttpMethod.Delete, "/venues/{venue}/schedules/{schedule}", true) },

        // Streams
        { "venues.streams.store", new ApiRoute("venues.streams.store", HttpMethod.Post, "/venues/{venue}/streams", true) },
        { "venues.streams.update", new ApiRoute("venues.streams.update", HttpMethod.Post, "/venues/{venue}/streams/{stream}", true) },
        { "venues.streams.logo", new ApiRoute("venues.streams.logo", HttpMethod.Post, "/venues/{venue}/streams/{stream}/logo", true) },
        { "venues.streams.destroy", new ApiRoute("venues.streams.destroy", HttpMethod.Delete, "/venues/{venue}/streams/{stream}", true) },

        // Mannequin
        { "mannequin.update", new ApiRoute("mannequin.update", HttpMethod.Post, "/mannequin/update", true) },

        // Mods
        { "mods.store", new ApiRoute("mods.store", HttpMethod.Post, "/mods", true) },
        { "mods.update", new ApiRoute("mods.update", HttpMethod.Post, "/mods/{mod}", true) },
        { "mods.destroy", new ApiRoute("mods.destroy", HttpMethod.Delete, "/mods/{mod}", true) },
        { "mods.versions.store", new ApiRoute("mods.versions.store", HttpMethod.Post, "/mods/{mod}/versions", true) },
        { "mods.versions.destroy", new ApiRoute("mods.versions.destroy", HttpMethod.Delete, "/mods/{mod}/versions/{version}", true) },

        // Location Mods
        { "venues.locations.mods.store", new ApiRoute("venues.locations.mods.store", HttpMethod.Post, "/venues/{venue}/locations/{location}/mods", true) },
        { "venues.locations.mods.update", new ApiRoute("venues.locations.mods.update", HttpMethod.Post, "/venues/{venue}/locations/{location}/mods/{mod}", true) },
        { "venues.locations.mods.destroy", new ApiRoute("venues.locations.mods.destroy", HttpMethod.Delete, "/venues/{venue}/locations/{location}/mods/{mod}", true) },

        // Houses
        { "houses.grant.store", new ApiRoute("houses.grant.store", HttpMethod.Post, "/houses/{house}/grant", true) },
        { "houses.grant.destroy", new ApiRoute("houses.grant.destroy", HttpMethod.Delete, "/houses/{house}/grant/{houseGrant}", true) },
    };

    public static ApiRoute Get(string key)
    {
        if (!Routes.TryGetValue(key, out var route))
            throw new KeyNotFoundException($"API route '{key}' is not defined.");
        return route;
    }
}
