using System;
using System.IO;

namespace WpfApp.Models
{
    public class SelectedFileItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;
        public string Extension => Path.GetExtension(FilePath).ToLowerInvariant();
        public long FileSizeBytes { get; set; }

        public string FileSizeFormatted
        {
            get
            {
                double kb = FileSizeBytes / 1024.0;
                if (kb < 1024.0) return $"{kb:F1} KB";
                double mb = kb / 1024.0;
                if (mb < 1024.0) return $"{mb:F2} MB";
                double gb = mb / 1024.0;
                return $"{gb:F2} GB";
            }
        }

        public string FileTypeTag => Extension.ToUpperInvariant().TrimStart('.');

        public string IconSymbol => Extension switch
        {
            ".hwp" => "📝",
            ".hwpx" => "📋",
            ".pdf" => "📑",
            _ => "📄"
        };

        public string TypeBadgeBackground => Extension switch
        {
            ".hwp" => "#EFF6FF",
            ".hwpx" => "#ECFDF5",
            ".pdf" => "#FEF2F2",
            _ => "#F1F5F9"
        };

        public string TypeBadgeForeground => Extension switch
        {
            ".hwp" => "#1D4ED8",
            ".hwpx" => "#047857",
            ".pdf" => "#B91C1C",
            _ => "#475569"
        };

        public string TypeBadgeBorder => Extension switch
        {
            ".hwp" => "#BFDBFE",
            ".hwpx" => "#A7F3D0",
            ".pdf" => "#FECACA",
            _ => "#CBD5E1"
        };
    }
}
