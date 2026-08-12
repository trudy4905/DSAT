using System;

namespace WpfApp.Models
{
    public class PartitionInfo
    {
        public int PartitionIndex { get; set; }
        public int SectorSize { get; set; } = 512;
        public ulong StartSector { get; set; }
        public ulong SectorCount { get; set; }
        public ulong PartitionSizeBytes => SectorCount * (ulong)(SectorSize > 0 ? SectorSize : 512);
        public string Filesystem { get; set; } = "Unknown";
        public bool IsSupported => Filesystem.StartsWith("NTFS", StringComparison.OrdinalIgnoreCase) ||
                                   Filesystem.StartsWith("FAT", StringComparison.OrdinalIgnoreCase) ||
                                   Filesystem.StartsWith("exFAT", StringComparison.OrdinalIgnoreCase) ||
                                   Filesystem.StartsWith("EXT", StringComparison.OrdinalIgnoreCase);
    }
}
