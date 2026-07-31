#include "DocumentAnalyzerBase.h"
#include <cstdio>
#include <cstring>

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
                                        DocumentAnalysisResult &outResult) {
  outResult.physicalSize = physicalSize;
  outResult.logicalSize = logicalSize;

  const uint64_t OVERLAY_THRESHOLD = 16;
  if (physicalSize > logicalSize + OVERLAY_THRESHOLD) {
    uint64_t overlayBytes = physicalSize - logicalSize;
    outResult.hasOverlay = 1;
    outResult.isNormal = 0;
    outResult.riskLevel = 2; // 2 = Danger (Red)
    outResult.findingCount = 1;
    outResult.overlaySize = overlayBytes;

    strncpy_s(outResult.statusMessage, sizeof(outResult.statusMessage), "EOF",
              _TRUNCATE);
  } else {
    outResult.hasOverlay = 0;
    outResult.isNormal = 1;
    outResult.riskLevel = 0; // 0 = Safe (Green)
    outResult.findingCount = 0;
    outResult.overlaySize = 0;
    strncpy_s(outResult.statusMessage, sizeof(outResult.statusMessage), "정상",
              _TRUNCATE);
  }
}
