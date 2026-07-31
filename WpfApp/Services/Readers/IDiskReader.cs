using System;
using System.IO;

namespace WpfApp.Services.Readers
{
    public interface IDiskReader : IDisposable
    {
        string TargetPath { get; }
        string ImageTypeTag { get; }
        long CalculateTotalSize();
        byte[] ReadHeaderSectors(int maxBytes = 2097152);
        Stream OpenReadStream();
    }
}
