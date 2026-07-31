using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp.Models
{
    public class DiskItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string DiskIndexStr { get; set; } = "디스크 0"; // e.g. "디스크 0", "디스크 1"
        public string ModelName { get; set; } = string.Empty; // e.g. "KINGSTON SNV3S1000G"
        public string SerialNumber { get; set; } = string.Empty; // e.g. "S/N: 0000_0000_0000_0000_0026_B76"
        public string DriveLetter { get; set; } = string.Empty; // e.g. "C:\"
        public string VolumeLabel { get; set; } = string.Empty; // e.g. "OS (C:)"
        public string DriveTypeStr { get; set; } = string.Empty; // e.g. "Fixed", "Removable"
        public string DeviceCategory { get; set; } = "로컬 장치"; // "로컬 장치" vs "외장 장치" vs "이미지 파일" vs "개별 파일"
        public bool IsExternalDevice { get; set; }
        
        public double TotalSizeGb { get; set; }
        public double FreeSpaceGb { get; set; }
        public double UsedSpaceGb => Math.Max(0, TotalSizeGb - FreeSpaceGb);

        public double UsagePercentage
        {
            get => TotalSizeGb > 0 ? (UsedSpaceGb / TotalSizeGb) * 100 : 0;
            set { }
        }

        public string CapacityFormatted
        {
            get
            {
                if (TotalSizeGb >= 1000) return $"{TotalSizeGb / 1024.0:F1} TB";
                if (TotalSizeGb >= 1.0) return $"{TotalSizeGb:F1} GB";
                double totalMb = TotalSizeGb * 1024.0;
                if (totalMb >= 1.0) return $"{totalMb:F1} MB";
                double totalKb = totalMb * 1024.0;
                return $"{totalKb:F0} KB";
            }
        }

        public string TotalSizeFormatted => $"{TotalSizeGb:F1} GB";
        public string FreeSpaceFormatted => $"{FreeSpaceGb:F1} GB";
        public string UsedSpaceFormatted => $"{UsedSpaceGb:F1} GB";

        public bool IsImageFile { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string ImageTypeTag { get; set; } = string.Empty; // e.g. "E01", "RAW", "DD"
        public int PartitionCount { get; set; } = 1;
        public string PartitionTypes { get; set; } = string.Empty; // e.g. "NTFS", "FAT32"

        public string PartitionCountFormatted => $"{PartitionCount}개";
        public string PartitionInfoFormatted => string.IsNullOrEmpty(PartitionTypes) ? $"{PartitionCount}개" : $"{PartitionCount}개 ({PartitionTypes})";
        public string ImageFormatName
        {
            get
            {
                if (string.IsNullOrEmpty(ImageTypeTag)) return "이미지 파일";
                string tagUpper = ImageTypeTag.ToUpperInvariant();
                return tagUpper switch
                {
                    "E01" or "EX01" => "Encase 이미지 파일",
                    "RAW" or "DD" or "IMG" => "RAW 이미지 파일",
                    "VHD" or "VMDK" => "가상 디스크 이미지",
                    _ => $"{tagUpper} 이미지 파일"
                };
            }
        }

        public bool IsDirectFilesMode { get; set; }
        public List<string> DirectFilePaths { get; set; } = new List<string>();

        public bool IsAddCard { get; set; } // True for "+ 이미지 추가" card
        public bool IsStandardCard => !IsAddCard;

        public string IconSymbol => IsAddCard ? "➕" : (IsDirectFilesMode ? "📄" : (IsImageFile ? "💿" : (IsExternalDevice ? "🔌" : "💽")));
        public string BadgeText => IsAddCard ? "ADD" : (IsDirectFilesMode ? "FILE" : (IsImageFile ? (string.IsNullOrEmpty(ImageTypeTag) ? "IMG" : ImageTypeTag) : DeviceCategory));
        public string BadgeColor => IsAddCard ? "#8B5CF6" : (IsDirectFilesMode ? "#059669" : (IsImageFile ? "#7C3AED" : (IsExternalDevice ? "#D97706" : "#2563EB")));
        public string BadgeBackground => IsExternalDevice ? "#FEF3C7" : "#EFF6FF";
        public string BadgeForeground => IsExternalDevice ? "#D97706" : "#2563EB";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
