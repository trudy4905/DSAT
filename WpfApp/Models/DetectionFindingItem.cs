using System;

namespace WpfApp.Models
{
    public class DetectionFindingItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RiskLevel { get; set; } // 0 = Safe, 1 = Caution, 2 = Danger, 3 = Critical

        public string BadgeText => RiskLevel switch
        {
            1 => "주의",
            2 => "위험",
            3 => "복합위험",
            _ => "정상"
        };

        public string BadgeColor => RiskLevel switch
        {
            1 => "#D97706", // Amber
            2 => "#DC2626", // Red
            3 => "#9333EA", // Purple
            _ => "#16A34A"  // Green
        };
    }
}
