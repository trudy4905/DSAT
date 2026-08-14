using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using WpfApp.Models;

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

        /// <summary>
        /// 현재 애플리케이션(WpfApp.exe)이 실행되고 있는 드라이브 루트(예: "E:\")를 반환합니다.
        /// 현장용 USB에서 실행 시 본인 USB 드라이브를 디스크 스캔 목록에서 자동 제외하는 용도입니다.
        /// </summary>
        public static string GetExecutionDriveRoot()
        {
            try
            {
                string execPath = Environment.ProcessPath
                    ?? AppContext.BaseDirectory;
                return Path.GetPathRoot(execPath) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static List<DiskItem> GetAvailableDisks()
        {
            var diskList = new List<DiskItem>();
            int displayIndex = 0;
            string execDriveRoot = GetExecutionDriveRoot();

            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();

                foreach (DriveInfo drive in drives)
                {
                    try
                    {
                        if (!drive.IsReady) continue;

                        // 본인 프로그램이 실행 중인 USB 드라이브는 현장 포렌식 무흔적(Self-Exclusion) 원칙에 따라 스킵
                        if (!string.IsNullOrEmpty(execDriveRoot) &&
                            string.Equals(drive.Name, execDriveRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

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
                            totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
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
