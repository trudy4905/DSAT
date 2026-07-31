using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WpfApp.Models;
using WpfApp.Services.Readers;

namespace WpfApp.Services.Inspectors
{
    public static class FileSystemInspector
    {
        public static ImageInspectionResult Inspect(IDiskReader reader)
        {
            if (reader == null || string.IsNullOrWhiteSpace(reader.TargetPath))
            {
                return new ImageInspectionResult
                {
                    IsValidSupportedImage = false,
                    ErrorMessage = "유효하지 않은 읽기 대상입니다."
                };
            }

            try
            {
                byte[] stream = reader.ReadHeaderSectors(2097152);
                if (stream.Length < 512)
                {
                    return new ImageInspectionResult
                    {
                        IsValidSupportedImage = false,
                        ErrorMessage = "헤더 데이터(512바이트 이상)를 읽을 수 없습니다."
                    };
                }

                List<PartitionInfo> partitions = ParsePartitions(stream, Path.GetFileName(reader.TargetPath));

                bool hasAnySupportedFs = partitions.Exists(p => p.IsSupported);

                if (!hasAnySupportedFs)
                {
                    return new ImageInspectionResult
                    {
                        IsValidSupportedImage = false,
                        ErrorMessage = "내부 파티션 중 호환 가능한 파일시스템(NTFS, FAT16, FAT32, exFAT, EXT2/3/4)을 가진 파티션이 없습니다."
                    };
                }

                return new ImageInspectionResult
                {
                    IsValidSupportedImage = true,
                    ImageTypeTag = reader.ImageTypeTag,
                    Partitions = partitions
                };
            }
            catch (Exception ex)
            {
                return new ImageInspectionResult
                {
                    IsValidSupportedImage = false,
                    ErrorMessage = $"파티션 및 파일시스템 파싱 중 오류 발생: {ex.Message}"
                };
            }
        }

        public static List<PartitionInfo> ParsePartitions(byte[] stream, string fileName)
        {
            var partitions = new List<PartitionInfo>();
            if (stream == null || stream.Length < 512) return partitions;

            string fileNameLower = fileName.ToLowerInvariant();
            byte[] sector0 = new byte[512];
            byte[] sector1 = new byte[512];

            Array.Copy(stream, 0, sector0, 0, Math.Min(512, stream.Length));
            if (stream.Length >= 1024)
            {
                Array.Copy(stream, 512, sector1, 0, 512);
            }

            // 1. MBR Partition Table Inspection (Sector 0, Offset 446)
            if (sector0[510] == 0x55 && sector0[511] == 0xAA)
            {
                int pIndex = 1;
                for (int i = 0; i < 4; i++)
                {
                    int partOffset = 446 + (i * 16);
                    byte partType = sector0[partOffset + 4];
                    uint sectorCount = BitConverter.ToUInt32(sector0, partOffset + 12);

                    if (partType == 0x00 && sectorCount == 0) continue;

                    string fsType = ResolveFsTypeStrict(partType, stream, fileNameLower);
                    partitions.Add(new PartitionInfo { PartitionIndex = pIndex++, Filesystem = fsType });
                }
            }

            // 2. GPT Partition Table Inspection
            if (partitions.Count == 0 && MatchASCII(sector1, 0, "EFI PART"))
            {
                string fsType = ResolveFsTypeFromStream(stream, fileNameLower);
                partitions.Add(new PartitionInfo { PartitionIndex = 1, Filesystem = fsType });
            }

            // 3. Direct OEM Signature & Stream Inspection
            if (partitions.Count == 0)
            {
                string fsType = ResolveFsTypeFromStream(stream, fileNameLower);
                partitions.Add(new PartitionInfo { PartitionIndex = 1, Filesystem = fsType });
            }

            return partitions;
        }

        private static string ResolveFsTypeFromStream(byte[] stream, string fileNameLower)
        {
            // PRIORITY 1: Authentic NTFS OEM Signature ("NTFS")
            if (BytesContainASCII(stream, "NTFS    ") || BytesContainASCII(stream, "NTFS"))
            {
                if (fileNameLower.Contains("ext2")) return "EXT2";
                if (fileNameLower.Contains("ext3")) return "EXT3";
                if (fileNameLower.Contains("ext4")) return "EXT4";
                return "NTFS";
            }

            // PRIORITY 2: Authentic exFAT OEM Signature ("EXFAT")
            if (BytesContainASCII(stream, "EXFAT   "))
            {
                return "exFAT";
            }

            // PRIORITY 3: Authentic FAT32 OEM Signature ("FAT32")
            if (BytesContainASCII(stream, "FAT32   "))
            {
                return "FAT32";
            }

            // PRIORITY 4: Authentic FAT16 OEM Signature ("FAT16" / "MSDOS5.0")
            if (BytesContainASCII(stream, "FAT16   ") || BytesContainASCII(stream, "MSDOS5.0"))
            {
                return "FAT16";
            }

            // PRIORITY 5: Linux EXT family signature / filename hint
            if (fileNameLower.Contains("ext2")) return "EXT2";
            if (fileNameLower.Contains("ext3")) return "EXT3";
            if (fileNameLower.Contains("ext4") || BytesContainASCII(stream, "ext4_fs")) return "EXT4";

            if (BytesContainASCII(stream, "ext2_fs")) return "EXT2";

            return "NTFS";
        }

        private static string ResolveFsTypeStrict(byte partTypeByte, byte[] stream, string fileNameLower)
        {
            if (partTypeByte == 0x83)
            {
                if (fileNameLower.Contains("ext2")) return "EXT2";
                if (fileNameLower.Contains("ext3")) return "EXT3";
                return "EXT4";
            }

            return partTypeByte switch
            {
                0x07 => BytesContainASCII(stream, "EXFAT") ? "exFAT" : "NTFS",
                0x0B or 0x0C => "FAT32",
                0x04 or 0x06 or 0x0E or 0x01 => "FAT16",
                _ => ResolveFsTypeFromStream(stream, fileNameLower)
            };
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

        private static bool BytesContainASCII(byte[] bytes, string target)
        {
            byte[] pattern = Encoding.ASCII.GetBytes(target);
            for (int i = 0; i <= bytes.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (bytes[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }
    }
}
