#include "CheckHwpMacro.h"

DetectionFinding CheckHwpMacro(FILE *fp, uint64_t fileSize) {
  (void)fp;
  (void)fileSize;

  DetectionFinding finding;
  finding.detected = false;
  finding.riskLevel = 3; // Critical (Purple)
  finding.ruleType = RULE_TYPE_MACRO_SCRIPT;
  finding.title = "VBA 악성 매크로 탐지";
  finding.description =
      "문서 내 자동 실행 스크립트(AutoOpen) 및 외부 명령 실행 코드 감지";

  return finding;
}
