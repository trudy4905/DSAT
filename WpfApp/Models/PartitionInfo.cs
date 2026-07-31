using System;

namespace WpfApp.Models
{
    public class PartitionInfo
    {
        public int PartitionIndex { get; set; }
        public string Filesystem { get; set; } = "Unknown";
        public bool IsSupported => Filesystem.StartsWith("NTFS", StringComparison.OrdinalIgnoreCase) ||
                                   Filesystem.StartsWith("FAT", StringComparison.OrdinalIgnoreCase) ||
                                   Filesystem.StartsWith("exFAT", StringComparison.OrdinalIgnoreCase) ||
                                   Filesystem.StartsWith("EXT", StringComparison.OrdinalIgnoreCase);
    }
}
