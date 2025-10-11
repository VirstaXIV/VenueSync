using System;
using VenueSync.Services.IPC;

namespace VenueSync.Services;

public class IPCManager: IDisposable
{
    public PenumbraIPC Penumbra { get; }
    
    public IPCManager(PenumbraIPC penumbra)
    {
        Penumbra = penumbra;
    }
    
    public void Dispose()
    {
    }
}
