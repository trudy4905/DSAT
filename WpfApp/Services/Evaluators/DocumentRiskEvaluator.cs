using System;
using System.Collections.Generic;
using WpfApp.Models;

namespace WpfApp.Services.Evaluators
{
    /// <summary>
    /// C++ Native Engine 구조 분석 결과를 바탕으로 위험도 레벨(0:Safe, 1:Caution, 2:Danger, 3:Critical)과
    /// 탐지 소견(DetectionFindingItem)을 통합 계산/평가하는 모듈입니다.
    /// </summary>
    public static class DocumentRiskEvaluator
    {
        public static void EvaluateDocument(HwpFileItem item, DocumentAnalysisResult analysis)
        {
            item.IsNormal = (analysis.IsNormal == 1);
            item.HasOverlay = (analysis.HasOverlay == 1);
            item.OverlaySizeBytes = (long)analysis.OverlaySize;

            var findings = new List<DetectionFindingItem>();

            if (analysis.HasOverlay == 1)
            {
                double kb = analysis.OverlaySize / 1024.0;
                string overlayStr = kb >= 1024.0 ? $"{kb / 1024.0:F2} MB" : $"{kb:F1} KB";

                findings.Add(new DetectionFindingItem
                {
                    Title = "데이터 오버레이 탐지",
                    Description = $"문서 마지막 오프셋 뒤에 {overlayStr} 크기의 데이터 발견",
                    RiskLevel = 2 // Danger (Red)
                });
            }

            item.Findings = findings;

            if (findings.Count > 1)
            {
                item.RiskLevel = 3; // Critical (Purple)
                item.StatusText = $"위험 ({findings.Count}건)";
            }
            else if (findings.Count == 1)
            {
                item.RiskLevel = findings[0].RiskLevel;
                item.StatusText = string.IsNullOrWhiteSpace(analysis.StatusMessage) ? "EOF" : analysis.StatusMessage;
            }
            else
            {
                item.RiskLevel = 0; // Safe (Green)
                item.StatusText = "정상";
            }
        }
    }
}
