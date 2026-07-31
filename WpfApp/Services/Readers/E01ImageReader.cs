using System;
using System.IO;
using System.Text;

namespace WpfApp.Services.Readers
{
    public class E01ImageReader : IDiskReader
    {
        public string TargetPath { get; }
        public string ImageTypeTag => "E01 Forensic Image";

        public E01ImageReader(string filePath)
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
            string ext = Path.GetExtension(TargetPath).ToLowerInvariant();

            string prefix = ext == ".e01" ? ".e" : ".ex";

            // 1) .e02 ~ .e99
            for (int i = 2; i <= 99; i++)
            {
                string nextFile = Path.Combine(dir, $"{baseName}{prefix}{i:D2}");
                if (File.Exists(nextFile)) totalBytes += new FileInfo(nextFile).Length;
                else return totalBytes;
            }

            // 2) .eaa ~ .ezz
            for (char c1 = 'a'; c1 <= 'z'; c1++)
            {
                for (char c2 = 'a'; c2 <= 'z'; c2++)
                {
                    string nextFile = Path.Combine(dir, $"{baseName}{prefix}{c1}{c2}");
                    if (File.Exists(nextFile)) totalBytes += new FileInfo(nextFile).Length;
                    else return totalBytes;
                }
            }

            return totalBytes;
        }

        public byte[] ReadHeaderSectors(int maxBytes = 2097152)
        {
            if (!File.Exists(TargetPath)) return Array.Empty<byte>();

            using var fs = new FileStream(TargetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long scanSize = Math.Min(maxBytes, fs.Length);
            byte[] stream = new byte[(int)scanSize];
            int bytesRead = fs.Read(stream, 0, stream.Length);

            if (bytesRead < 512) return Array.Empty<byte>();

            bool isE01 = MatchASCII(stream, 0, "EVF") || MatchASCII(stream, 0, "LVF") || MatchASCII(stream, 0, "EWF");

            if (isE01)
            {
                long offset = 13;
                while (offset + 76 <= fs.Length)
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    byte[] secHeader = new byte[76];
                    if (fs.Read(secHeader, 0, 76) < 76) break;

                    string secType = Encoding.ASCII.GetString(secHeader, 0, 16).TrimEnd('\0', ' ');
                    long nextOffset = BitConverter.ToInt64(secHeader, 16);

                    if (secType.Equals("volume", StringComparison.OrdinalIgnoreCase) ||
                        secType.Equals("disk", StringComparison.OrdinalIgnoreCase) ||
                        secType.Equals("sectors", StringComparison.OrdinalIgnoreCase) ||
                        secType.Equals("data", StringComparison.OrdinalIgnoreCase))
                    {
                        long dataOffset = offset + 76;
                        fs.Seek(dataOffset, SeekOrigin.Begin);
                        byte[] volumeBuffer = new byte[Math.Min(maxBytes, (int)(fs.Length - dataOffset))];
                        int vRead = fs.Read(volumeBuffer, 0, volumeBuffer.Length);
                        if (vRead < volumeBuffer.Length)
                        {
                            Array.Resize(ref volumeBuffer, vRead);
                        }
                        return volumeBuffer;
                    }

                    if (nextOffset <= offset || nextOffset >= fs.Length) break;
                    offset = nextOffset;
                }
            }

            if (bytesRead < stream.Length)
            {
                Array.Resize(ref stream, bytesRead);
            }
            return stream;
        }

        public Stream OpenReadStream()
        {
            return new FileStream(TargetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        private static bool MatchASCII(byte[] bytes, int offset, string target)
        {
            byte[] pattern = Encoding.ASCII.GetBytes(target);
            if (offset < 0 || offset + pattern.Length > bytes.Length) return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (bytes[offset + i] != pattern[i]) return false;
            }
            return true;
        }

        public void Dispose()
        {
        }
    }
}
