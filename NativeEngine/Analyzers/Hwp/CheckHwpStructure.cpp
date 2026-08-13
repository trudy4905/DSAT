#include "CheckHwpStructure.h"

DetectionFinding CheckHwpStructure(FILE *fp, uint64_t fileSize) {
  (void)fp;
  (void)fileSize;

  DetectionFinding finding;
  finding.detected = false;
  finding.riskLevel = 2; // Danger (Red)
  finding.ruleType = RULE_TYPE_STRUCTURE_ANOMALY;
  finding.title = "문서 구조 변조 탐지";
  finding.description =
      "OLE Header 내 비정상 섹터 크기 및 손상된 Entry 포인터 발견";

  return finding;
}
