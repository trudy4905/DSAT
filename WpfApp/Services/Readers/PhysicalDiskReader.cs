using System;
using System.IO;

namespace WpfApp.Services.Readers
{
    public class PhysicalDiskReader : IDiskReader
    {
        public string TargetPath { get; }
        public string ImageTypeTag => "Physical / Logical Disk";

        public PhysicalDiskReader(string driveLetterOrPath)
        {
            TargetPath = driveLetterOrPath;
        }

        public long CalculateTotalSize()
        {
            try
            {
                var driveInfo = new DriveInfo(TargetPath);
                return driveInfo.IsReady ? driveInfo.TotalSize : 0;
            }
            catch
            {
                return 0;
            }
        }

        public byte[] ReadHeaderSectors(int maxBytes = 2097152)
        {
            try
            {
                string letter = TargetPath.TrimEnd('\\');
                string path = $@"\\.\{letter}";
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] buffer = new byte[Math.Min(maxBytes, 65536)];
                int read = fs.Read(buffer, 0, buffer.Length);
                if (read < buffer.Length)
                {
                    Array.Resize(ref buffer, read);
                }
                return buffer;
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        public Stream OpenReadStream()
        {
            string letter = TargetPath.TrimEnd('\\');
            string path = $@"\\.\{letter}";
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public void Dispose()
        {
        }
    }
}
