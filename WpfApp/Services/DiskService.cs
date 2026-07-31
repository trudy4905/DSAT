using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using WpfApp.Models;
using WpfApp.Services.Readers;

namespace WpfApp.Services
{
    public static class DiskService
    {
        /// <summary>
        /// 논리 드라이브 문자(예: "C")로 연결된 물리 디스크의 Model과 SerialNumber를
        /// WMI 연결 체인(LogicalDisk → Partition → DiskDrive)으로 직접 조회합니다.
        /// </summary>
        private static (string model, string serial) GetPhysicalDiskInfo(string driveLetter)
        {
            string letter = driveLetter.TrimEnd('\\');

            try
            {
                using var ldQuery = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{letter}'}} " +
                    "WHERE AssocClass=Win32_LogicalDiskToPartition");

                foreach (ManagementObject partition in ldQuery.Get())
                {
                    using var driveQuery = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} " +
                        "WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                    foreach (ManagementObject diskDrive in driveQuery.Get())
                    {
                        string model  = diskDrive["Model"]?.ToString()?.Trim()        ?? string.Empty;
                        string serial = diskDrive["SerialNumber"]?.ToString()?.Trim() ?? string.Empty;
                        return (model, serial);
                    }
                }
            }
            catch { }

            return (string.Empty, string.Empty);
        }

        public static List<DiskItem> GetAvailableDisks()
        {
            var diskList = new List<DiskItem>();
            int displayIndex = 0;

            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();

                foreach (DriveInfo drive in drives)
                {
                    try
                    {
                        if (!drive.IsReady) continue;

                        string driveLetterTrimmed = drive.Name.TrimEnd('\\');
                        bool   isExternal         = drive.DriveType == DriveType.Removable || drive.DriveType == DriveType.Network;
                        string deviceCategory     = isExternal ? "외장 장치" : "로컬 장치";

                        var (physModel, physSerial) = GetPhysicalDiskInfo(drive.Name);

                        string rawLabel = string.Empty;
                        try { rawLabel = drive.VolumeLabel; } catch { }

                        double totalGb = 0;
                        double freeGb  = 0;
                        try
                        {
                            using var reader = new PhysicalDiskReader(drive.Name);
                            long totalBytes = reader.CalculateTotalSize();
                            totalGb = totalBytes / (1024.0 * 1024.0 * 1024.0);
                            freeGb  = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        }
                        catch { }

                        string defaultCategoryName = isExternal ? "외장 디스크" : "로컬 디스크";
                        string displayName = !string.IsNullOrWhiteSpace(physModel) ? physModel : defaultCategoryName;

                        string volumeLabel = string.IsNullOrWhiteSpace(rawLabel)
                            ? $"{displayName} ({driveLetterTrimmed})"
                            : $"{rawLabel} ({driveLetterTrimmed})";

                        diskList.Add(new DiskItem
                        {
                            DiskIndexStr    = $"디스크 {displayIndex}",
                            ModelName       = string.IsNullOrEmpty(physModel) ? volumeLabel : physModel,
                            SerialNumber    = string.IsNullOrEmpty(physSerial) ? "S/N: Unknown" : physSerial,
                            DriveLetter     = drive.Name,
                            VolumeLabel     = volumeLabel,
                            DriveTypeStr    = $"{drive.DriveType} Drive",
                            DeviceCategory  = deviceCategory,
                            IsExternalDevice = isExternal,
                            TotalSizeGb     = totalGb,
                            FreeSpaceGb     = freeGb,
                            IsImageFile     = false,
                            IsAddCard       = false,
                            IsSelected      = false
                        });

                        displayIndex++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading drive {drive.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving drive list: {ex.Message}");
            }

            return diskList;
        }
    }
}
