using System;
using System.Linq;
using Dalamud.Plugin;
using OtterGui.Services;

namespace VenueSync.Services;

/* Parts of this code from ECommons DalamudReflector (Brought from Snowcloak)

MIT License

Copyright (c) 2023 NightmareXIV

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

public record PluginRecord()
{
    public required string InternalName { get; set; }
    public required Version Version { get; set; }
    public bool IsLoaded { get; set; }
};

public class PluginWatcherService: IService
{
    public PluginWatcherService()
    {
        
    }
    
    public static PluginRecord? GetInitialPluginState(IDalamudPluginInterface pluginInterface, string internalName)
    {
        try
        {
            var plugin = pluginInterface.InstalledPlugins.Where(p => p.InternalName.Equals(internalName, StringComparison.Ordinal))
                                        .OrderBy(p => (!p.IsLoaded, p.Version))
                                        .FirstOrDefault();

            if (plugin == null)
                return null;

            return new PluginRecord()
            {
                InternalName = internalName,
                Version = plugin.Version,
                IsLoaded = plugin.IsLoaded,
            };
        }
        catch
        {
            return null;
        }
    }
}
