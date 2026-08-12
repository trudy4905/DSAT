using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WpfApp.Models;

namespace WpfApp.Services
{
    public static class DiskImageService
    {
        private static readonly HashSet<string> StandardExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".e01", ".ex01", ".raw", ".dd", ".001"
        };

        public static bool IsSupportedImageExtension(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (StandardExtensions.Contains(ext)) return true;

            string fileName = Path.GetFileName(filePath);
            if (Regex.IsMatch(fileName, @"\.(?:e\d{2}|ex\d{2}|e[a-z]{2}|\d{3,4})$", RegexOptions.IgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static ImageInspectionResult InspectImageFileSystems(string filePath)
        {
            Console.WriteLine($"[C# DiskImageService] InspectImageFileSystems called for: {filePath}");

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine("[C# DiskImageService] File does not exist!");
                return new ImageInspectionResult
                {
                    IsValidSupportedImage = false,
                    ErrorMessage = "파일을 찾을 수 없거나 접근할 수 없습니다."
                };
            }

            if (!IsSupportedImageExtension(filePath))
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                Console.WriteLine($"[C# DiskImageService] Unsupported extension: {ext}");
                return new ImageInspectionResult
                {
                    IsValidSupportedImage = false,
                    ErrorMessage = $"지원하지 않는 이미지 확장자입니다 ({ext}). (E01 및 DD/RAW 포렌식 이미지 파일만 지원)"
                };
            }

            try
            {
                Console.WriteLine("[C# DiskImageService] Calling NativeBridge.Engine_InspectForensicImage...");
                // Call NativeEngine (libewf / libtsk engine)
                int res = NativeBridge.Engine_InspectForensicImage(filePath, out var nativeOutput);
                Console.WriteLine($"[C# DiskImageService] NativeBridge returned res={res}, isValid={nativeOutput.IsValid}, err={nativeOutput.ErrorMessage}");
                if (res == 0 && nativeOutput.IsValid)
                {
                    var partitionList = new List<PartitionInfo>();
                    for (int i = 0; i < nativeOutput.PartitionCount; i++)
                    {
                        var p = nativeOutput.Partitions[i];
                        partitionList.Add(new PartitionInfo
                        {
                            PartitionIndex = p.PartitionIndex,
                            SectorSize = p.SectorSize,
                            StartSector = p.StartSector,
                            SectorCount = p.SectorCount,
                            Filesystem = p.Filesystem
                        });
                    }

                    return new ImageInspectionResult
                    {
                        IsValidSupportedImage = true,
                        ImageTypeTag = nativeOutput.ImageTypeTag,
                        TotalImageSize = nativeOutput.TotalImageSize,
                        TotalPartitionSize = nativeOutput.TotalPartitionSize,
                        Partitions = partitionList
                    };
                }

                return new ImageInspectionResult
                {
                    IsValidSupportedImage = false,
                    ErrorMessage = !string.IsNullOrWhiteSpace(nativeOutput.ErrorMessage) ? nativeOutput.ErrorMessage : "Native C++ 포렌식 엔진 분석 실패"
                };
            }
            catch (Exception ex)
            {
                return new ImageInspectionResult
                {
                    IsValidSupportedImage = false,
                    ErrorMessage = $"Native 포렌식 엔진 검사 오류: {ex.Message}"
                };
            }
        }

        public static ulong GetPhysicalImageFileSetSize(string primaryFilePath)
        {
            if (string.IsNullOrWhiteSpace(primaryFilePath) || !File.Exists(primaryFilePath)) return 0;

            try
            {
                FileInfo fi = new FileInfo(primaryFilePath);
                ulong totalSize = (ulong)fi.Length;

                string ext = fi.Extension;
                if (Regex.IsMatch(ext, @"\.(?:e\d{2}|ex\d{2}|e[a-z]{2}|\d{3,4})$", RegexOptions.IgnoreCase))
                {
                    string? dir = fi.DirectoryName;
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        string baseName = Path.GetFileNameWithoutExtension(primaryFilePath);
                        var matchedFiles = Directory.GetFiles(dir, baseName + ".*");
                        totalSize = 0;
                        foreach (var f in matchedFiles)
                        {
                            FileInfo itemFi = new FileInfo(f);
                            string itemExt = itemFi.Extension;
                            if (Regex.IsMatch(itemExt, @"\.(?:e\d{2}|ex\d{2}|e[a-z]{2}|\d{3,4})$", RegexOptions.IgnoreCase))
                            {
                                totalSize += (ulong)itemFi.Length;
                            }
                        }
                    }
                }
                return totalSize > 0 ? totalSize : (ulong)fi.Length;
            }
            catch
            {
                try { return (ulong)new FileInfo(primaryFilePath).Length; } catch { return 0; }
            }
        }

        public static DiskItem? CreateDiskItemFromImageFile(string filePath, ImageInspectionResult inspection)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || !inspection.IsValidSupportedImage)
                return null;

            try
            {
                ulong physicalFileSizeBytes = GetPhysicalImageFileSetSize(filePath);
                ulong logicalPartSizeBytes = inspection.TotalPartitionSize > 0 ? inspection.TotalPartitionSize :
                                             (inspection.TotalImageSize > 0 ? inspection.TotalImageSize : physicalFileSizeBytes);
                double totalGb = logicalPartSizeBytes / (1024.0 * 1024.0 * 1024.0);

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
                    ImageFileSizeBytes = physicalFileSizeBytes,
                    TotalPartitionSizeBytes = logicalPartSizeBytes,
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
