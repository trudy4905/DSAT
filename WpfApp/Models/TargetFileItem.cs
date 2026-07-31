using System;
using System.IO;

namespace WpfApp.Models
{
    public class TargetFileItem
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string Extension { get; set; } = string.Empty;

        public bool IsPdf => Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        public bool IsHwpx => Extension.Equals(".hwpx", StringComparison.OrdinalIgnoreCase);
        public string FileTypeBadge => IsPdf ? "PDF" : (IsHwpx ? "HWPX" : (Extension.Equals(".hwp", StringComparison.OrdinalIgnoreCase) ? "HWP" : "FILE"));
        public string BadgeColor => IsPdf ? "#DC2626" : (IsHwpx ? "#7C3AED" : "#D97706");

        public string FileSizeFormatted
        {
            get
            {
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
                return $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB";
            }
        }

        public static TargetFileItem FromPath(string filePath)
        {
            var fi = new FileInfo(filePath);
            return new TargetFileItem
            {
                FileName = fi.Name,
                FilePath = fi.FullName,
                FileSizeBytes = fi.Exists ? fi.Length : 0,
                Extension = fi.Extension.ToLowerInvariant()
            };
        }
    }
}
