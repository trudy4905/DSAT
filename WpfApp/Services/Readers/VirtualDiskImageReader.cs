using System;
using System.IO;

namespace WpfApp.Services.Readers
{
    public class VirtualDiskImageReader : IDiskReader
    {
        public string TargetPath { get; }
        public string ImageTypeTag => "Virtual Disk Image";

        public VirtualDiskImageReader(string filePath)
        {
            TargetPath = filePath;
        }

        public long CalculateTotalSize()
        {
            if (string.IsNullOrWhiteSpace(TargetPath) || !File.Exists(TargetPath))
                return 0;

            return new FileInfo(TargetPath).Length;
        }

        public byte[] ReadHeaderSectors(int maxBytes = 2097152)
        {
            if (!File.Exists(TargetPath)) return Array.Empty<byte>();

            using var fs = new FileStream(TargetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long scanSize = Math.Min(maxBytes, fs.Length);
            byte[] buffer = new byte[(int)scanSize];
            int bytesRead = fs.Read(buffer, 0, buffer.Length);
            if (bytesRead < buffer.Length)
            {
                Array.Resize(ref buffer, bytesRead);
            }
            return buffer;
        }

        public Stream OpenReadStream()
        {
            return new FileStream(TargetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public void Dispose()
        {
        }
    }
}
