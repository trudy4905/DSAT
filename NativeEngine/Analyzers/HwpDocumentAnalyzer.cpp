#include "HwpDocumentAnalyzer.h"
#include "Hwp/CheckHwpMacro.h"
#include "Hwp/CheckHwpOleOverlay.h"
#include "Hwp/CheckHwpOleSlack.h"
#include <cstring>
#include <vector>

bool HwpDocumentAnalyzer::Analyze(FILE *fp, uint64_t fileSize,
                                  DocumentAnalysisResult &outResult) {
  strncpy_s(outResult.detectedFormat, sizeof(outResult.detectedFormat), "HWP",
            _TRUNCATE);

  std::vector<DetectionFinding> findings;
  uint64_t logicalSize = fileSize;

  // 기법 1: OLE 오버레이 탐지
  DetectionFinding f1 = CheckHwpOleOverlay(fp, fileSize, logicalSize);
  if (f1.detected) {
    findings.push_back(f1);
  }

  // 기법 2: OLE 컨테이너 슬랙/은닉 탐지
  DetectionFinding f2 = CheckHwpOleSlack(fp, fileSize);
  if (f2.detected) {
    findings.push_back(f2);
  }

  // 기법 3: VBA/JScript 매크로 탐지
  DetectionFinding f3 = CheckHwpMacro(fp, fileSize);
  if (f3.detected) {
    findings.push_back(f3);
  }

  // 결과 종합 판단
  FormatResult(fileSize, logicalSize, findings, outResult);

  return true;
}
