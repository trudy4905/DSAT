using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp.Models
{
    public class HwpFileItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _index;
        private string _textSnippet = string.Empty;
        private bool _isNormal = true;
        private bool _hasOverlay = false;
        private int _riskLevel = 0; // 0=Safe, 1=Caution, 2=Danger, 3=Critical
        private long _overlaySizeBytes = 0;
        private string _statusText = "정상";
        private string _statusBadgeColor = "#16A34A"; // Green
        private List<DetectionFindingItem> _findings = new();

        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string DirectoryPath => System.IO.Path.GetDirectoryName(FilePath) ?? string.Empty;
        public string Extension { get; set; } = string.Empty; // ".hwp", ".hwpx", ".pdf"
        public long FileSizeBytes { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; }

        public bool IsPdf => Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        public bool IsHwpx => Extension.Equals(".hwpx", StringComparison.OrdinalIgnoreCase);
        public string FileTypeBadge => IsPdf ? "PDF" : (IsHwpx ? "HWPX" : "HWP");
        public string BadgeColor => IsPdf ? "#DC2626" : (IsHwpx ? "#7C3AED" : "#D97706");

        #region Status & Anomaly Overlay Properties
        public bool IsNormal
        {
            get => _isNormal;
            set
            {
                if (_isNormal != value)
                {
                    _isNormal = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasOverlay
        {
            get => _hasOverlay;
            set
            {
                if (_hasOverlay != value)
                {
                    _hasOverlay = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RiskLevel
        {
            get => _riskLevel;
            set
            {
                if (_riskLevel != value)
                {
                    _riskLevel = value;
                    OnPropertyChanged();
                    UpdateStatusBadgeColor();
                }
            }
        }

        public List<DetectionFindingItem> Findings
        {
            get => _findings;
            set
            {
                _findings = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasFindings));
                OnPropertyChanged(nameof(FindingCountText));
            }
        }

        public bool HasFindings => Findings.Count > 0;
        public string FindingCountText => $"탐지된 이상 항목 ({Findings.Count}건)";

        public long OverlaySizeBytes
        {
            get => _overlaySizeBytes;
            set
            {
                if (_overlaySizeBytes != value)
                {
                    _overlaySizeBytes = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusBadgeColor
        {
            get => _statusBadgeColor;
            set
            {
                if (_statusBadgeColor != value)
                {
                    _statusBadgeColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateStatusBadgeColor()
        {
            StatusBadgeColor = RiskLevel switch
            {
                1 => "#D97706", // Amber (Caution)
                2 => "#DC2626", // Red (Danger)
                3 => "#9333EA", // Purple (Critical)
                _ => "#16A34A"  // Green (Safe)
            };
        }
        #endregion

        public string TextSnippet
        {
            get => _textSnippet;
            set
            {
                if (_textSnippet != value)
                {
                    _textSnippet = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FileSizeFormatted
        {
            get
            {
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
                return $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB";
            }
        }

        public string CreatedTimeFormatted => CreatedTime.ToString("yyyy-MM-dd HH:mm");
        public string LastModifiedFormatted => LastModified.ToString("yyyy-MM-dd HH:mm");

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
