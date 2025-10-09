using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using System;
using System.Threading.Tasks;

namespace VenueSync;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IHost _host;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        
    }
    
    public void Dispose()
    {

    }
}
