#include "DocumentAnalyzerBase.h"
#include <cstdio>
#include <cstring>
#include <algorithm>

uint16_t DocumentAnalyzerBase::ReadU16(const uint8_t *p) {
  return static_cast<uint16_t>(p[0] | (p[1] << 8));
}

uint32_t DocumentAnalyzerBase::ReadU32(const uint8_t *p) {
  return static_cast<uint32_t>(p[0] | (p[1] << 8) | (p[2] << 16) |
                               (p[3] << 24));
}

bool DocumentAnalyzerBase::MatchBytes(const uint8_t *buffer, size_t bufferLen,
                                      size_t offset, const uint8_t *target,
                                      size_t targetLen) {
  if (offset + targetLen > bufferLen)
    return false;
  return memcmp(buffer + offset, target, targetLen) == 0;
}

void DocumentAnalyzerBase::FormatResult(uint64_t physicalSize,
                                         uint64_t logicalSize,
                                         const std::vector<DetectionFinding>& findings,
                                         DocumentAnalysisResult &outResult) {
  outResult.physicalSize = physicalSize;
  outResult.logicalSize = logicalSize;
  outResult.overlaySize = (physicalSize > logicalSize) ? (physicalSize - logicalSize) : 0;

  int count = static_cast<int>(findings.size());
  outResult.findingCount = std::min(count, 8); // 최대 8개 저장

  if (findings.empty()) {
    outResult.isNormal = 1;
    outResult.hasOverlay = 0;
    outResult.riskLevel = 0; // Safe (Green)
    strncpy_s(outResult.statusMessage, sizeof(outResult.statusMessage), "정상", _TRUNCATE);
  } else {
    outResult.isNormal = 0;
    int maxRisk = 0;
    bool hasOverlay = false;
    std::string summaryStr = "";

    for (int i = 0; i < outResult.findingCount; ++i) {
      const auto& f = findings[i];

      outResult.findings[i].riskLevel = f.riskLevel;
      outResult.findings[i].ruleType = static_cast<int32_t>(f.ruleType);
      strncpy_s(outResult.findings[i].title, sizeof(outResult.findings[i].title), f.title.c_str(), _TRUNCATE);
      strncpy_s(outResult.findings[i].description, sizeof(outResult.findings[i].description), f.description.c_str(), _TRUNCATE);

      if (f.riskLevel > maxRisk) maxRisk = f.riskLevel;
      if (f.ruleType == RULE_TYPE_OVERLAY) hasOverlay = true;

      if (!summaryStr.empty()) summaryStr += ", ";
      summaryStr += f.title;
    }

    outResult.riskLevel = maxRisk;
    outResult.hasOverlay = hasOverlay ? 1 : 0;

    // C++ 엔진에서 테이블/결과창에 표시할 최종 요약 텍스트 결정
    if (outResult.findingCount > 1) {
      char countBuf[64];
      sprintf_s(countBuf, sizeof(countBuf), "탐지 (%d건)", outResult.findingCount);
      strncpy_s(outResult.statusMessage, sizeof(outResult.statusMessage), countBuf, _TRUNCATE);
    } else {
      strncpy_s(outResult.statusMessage, sizeof(outResult.statusMessage), summaryStr.c_str(), _TRUNCATE);
    }
  }
}
