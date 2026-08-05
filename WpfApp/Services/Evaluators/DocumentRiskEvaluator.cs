using System;
using System.Collections.Generic;
using WpfApp.Models;

namespace WpfApp.Services.Evaluators
{
    /// <summary>
    /// C++ Native Engine 구조 분석 결과를 가공 없이 C# UI 데이터 모델에 그대로 전달 바인딩하는 평가 모듈입니다.
    /// 모든 결과 판단 및 텍스트 구성은 C++ Native Engine에서 전담합니다.
    /// </summary>
    public static class DocumentRiskEvaluator
    {
        public static void EvaluateDocument(HwpFileItem item, DocumentAnalysisResult analysis)
        {
            // 1. C++ Native Engine이 판단한 상태값 100% 그대로 대입
            item.IsNormal = (analysis.IsNormal == 1);
            item.HasOverlay = (analysis.HasOverlay == 1);
            item.RiskLevel = analysis.RiskLevel;
            item.OverlaySizeBytes = (long)analysis.OverlaySize;
            item.StatusText = analysis.StatusMessage;

            // 2. C++ Native Engine이 생성한 개별 탐지 카드 목록 그대로 대입
            var findings = new List<DetectionFindingItem>();

            if (analysis.FindingCount > 0 && analysis.Findings != null)
            {
                int count = Math.Min(analysis.FindingCount, analysis.Findings.Length);
                for (int i = 0; i < count; i++)
                {
                    var nativeFinding = analysis.Findings[i];
                    if (string.IsNullOrWhiteSpace(nativeFinding.Title)) continue;

                    findings.Add(new DetectionFindingItem
                    {
                        Title = nativeFinding.Title,
                        Description = nativeFinding.Description,
                        RiskLevel = nativeFinding.RiskLevel,
                        RuleType = (DetectionRuleType)nativeFinding.RuleType
                    });
                }
            }

            item.Findings = findings;
        }
    }
}
