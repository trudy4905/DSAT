using System;
using WpfApp.Services;

namespace WpfApp.Models
{
    public class DetectionFindingItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RiskLevel { get; set; } // 0 = Safe, 1 = Caution, 2 = Danger, 3 = Critical
        public DetectionRuleType RuleType { get; set; } = DetectionRuleType.None;


        public string BadgeText => RiskLevel switch
        {
            1 => "주의",
            2 => "위험",
            3 => "심각",
            _ => "정상"
        };

        public string BadgeColor => RiskLevel switch
        {
            1 => "#F59E0B", // Amber (주의)
            2 => "#EA580C", // Orange-Red (위험)
            3 => "#DC2626", // Red (심각)
            _ => "#16A34A"  // Green (정상)
        };
    }
}
