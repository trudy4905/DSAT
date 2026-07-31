using System;
using System.IO;

namespace WpfApp.Services.Readers
{
    public class RawImageReader : IDiskReader
    {
        public string TargetPath { get; }
        public string ImageTypeTag => Path.GetExtension(TargetPath).Equals(".iso", StringComparison.OrdinalIgnoreCase)
            ? "ISO Image"
            : "RAW Disk Image";

        public RawImageReader(string filePath)
        {
            TargetPath = filePath;
        }

        public long CalculateTotalSize()
        {
            if (string.IsNullOrWhiteSpace(TargetPath) || !File.Exists(TargetPath))
                return 0;

            long totalBytes = new FileInfo(TargetPath).Length;
            string dir = Path.GetDirectoryName(TargetPath) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(TargetPath);

            // .002, .003, ... .999+ 순차 탐색
            int idx = 2;
            while (true)
            {
                string nextFile = Path.Combine(dir, $"{baseName}.{idx:D3}");
                if (File.Exists(nextFile))
                {
                    totalBytes += new FileInfo(nextFile).Length;
                    idx++;
                }
                else
                {
                    break;
                }
            }

            return totalBytes;
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
