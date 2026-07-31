using System;
using System.Collections.Generic;
using System.IO;
using WpfApp.Models;
using WpfApp.Services.Inspectors;
using WpfApp.Services.Readers;

namespace WpfApp.Services
{
    public static class DiskImageService
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".e01", ".ex01", ".raw", ".dd", ".img", ".iso", ".vhd", ".vmdk"
        };

        public static IDiskReader CreateReader(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".e01" or ".ex01" => new E01ImageReader(filePath),
                ".vhd" or ".vmdk" => new VirtualDiskImageReader(filePath),
                _ => new RawImageReader(filePath)
            };
        }

        public static ImageInspectionResult InspectImageFileSystems(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new ImageInspectionResult
                {
                    IsValidSupportedImage = false,
                    ErrorMessage = "파일을 찾을 수 없거나 접근할 수 없습니다."
                };
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext))
            {
                return new ImageInspectionResult
                {
                    IsValidSupportedImage = false,
                    ErrorMessage = $"지원하지 않는 이미지 확장자입니다 ({ext}). (.E01, .Ex01, .raw, .dd, .img, .iso, .vhd, .vmdk 지원)"
                };
            }

            try
            {
                using var reader = CreateReader(filePath);
                return FileSystemInspector.Inspect(reader);
            }
            catch (Exception ex)
            {
                return new ImageInspectionResult
                {
                    IsValidSupportedImage = false,
                    ErrorMessage = $"이미지 파티션 파싱 중 오류가 발생했습니다: {ex.Message}"
                };
            }
        }

        public static DiskItem? CreateDiskItemFromImageFile(string filePath, ImageInspectionResult inspection)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || !inspection.IsValidSupportedImage)
                return null;

            try
            {
                using var reader = CreateReader(filePath);
                long totalBytes = 0;
                try { totalBytes = reader.CalculateTotalSize(); } catch { }
                if (totalBytes <= 0)
                {
                    try { totalBytes = new FileInfo(filePath).Length; } catch { }
                }
                double totalGb = totalBytes / (1024.0 * 1024.0 * 1024.0);

                var supportedFilesystems = inspection.Partitions
                    .Where(p => p.IsSupported && !string.IsNullOrWhiteSpace(p.Filesystem))
                    .Select(p => p.Filesystem)
                    .Distinct();
                string fsTypesStr = string.Join(", ", supportedFilesystems);

                return new DiskItem
                {
                    DriveLetter = filePath,
                    VolumeLabel = Path.GetFileName(filePath),
                    ModelName = $"{inspection.ImageTypeTag} ({fsTypesStr})",
                    DriveTypeStr = fsTypesStr,
                    TotalSizeGb = totalGb,
                    FreeSpaceGb = 0,
                    IsImageFile = true,
                    ImagePath = filePath,
                    ImageTypeTag = inspection.ImageTypeTag,
                    PartitionCount = Math.Max(1, inspection.SupportedPartitionCount),
                    PartitionTypes = fsTypesStr,
                    SerialNumber = filePath,
                    IsAddCard = false,
                    IsSelected = false
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating image disk item: {ex.Message}");
                return null;
            }
        }
    }
}
