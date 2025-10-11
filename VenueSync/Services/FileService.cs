using System;
using System.IO;

namespace VenueSync.Services;

public class FileService: IDisposable
{
    public FileService()
    {
        
    }

    public void Dispose()
    {
        
    }
    
    public bool IsDirectoryWritable(string dirPath, bool throwIfFails = false)
    {
        try
        {
            using FileStream fs = File.Create(
                Path.Combine(
                    dirPath,
                    Path.GetRandomFileName()
                ),
                1,
                FileOptions.DeleteOnClose);
            return true;
        }
        catch
        {
            if (throwIfFails)
                throw;

            return false;
        }
    }
}
