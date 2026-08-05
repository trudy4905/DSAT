#include "CheckHwpxZipOverlay.h"
#include <algorithm>
#include <cstdio>
#include <vector>

DetectionFinding CheckHwpxZipOverlay(FILE *fp, uint64_t fileSize,
                                     uint64_t &outLogicalSize) {
  DetectionFinding finding;
  finding.detected = false;

  size_t checkLen = static_cast<size_t>(std::min<uint64_t>(65557, fileSize));
  if (checkLen < 22) {
    outLogicalSize = fileSize;
    return finding;
  }

  std::vector<uint8_t> buffer(checkLen);
  if (_fseeki64(fp, fileSize - checkLen, SEEK_SET) != 0) {
    outLogicalSize = fileSize;
    return finding;
  }

  size_t readBytes = fread(buffer.data(), 1, checkLen, fp);
  if (readBytes < 22) {
    outLogicalSize = fileSize;
    return finding;
  }

  int foundIdx = -1;
  for (int i = static_cast<int>(readBytes) - 22; i >= 0; --i) {
    if (buffer[i] == 0x50 && buffer[i + 1] == 0x4B && buffer[i + 2] == 0x05 &&
        buffer[i + 3] == 0x06) {
      foundIdx = i;
      break;
    }
  }

  if (foundIdx < 0) {
    outLogicalSize = fileSize;
    return finding;
  }

  uint16_t commentLen = static_cast<uint16_t>(buffer[foundIdx + 20] |
                                              (buffer[foundIdx + 21] << 8));
  uint64_t eocdEnd = (fileSize - checkLen) + foundIdx + 22 + commentLen;
  outLogicalSize = std::min<uint64_t>(eocdEnd, fileSize);

  const uint64_t OVERLAY_THRESHOLD = 16;
  if (fileSize > outLogicalSize + OVERLAY_THRESHOLD) {
    uint64_t overlayBytes = fileSize - outLogicalSize;
    double kb = overlayBytes / 1024.0;
    char sizeBuf[64];
    if (kb >= 1024.0) {
      sprintf_s(sizeBuf, sizeof(sizeBuf), "%.2f MB", kb / 1024.0);
    } else {
      sprintf_s(sizeBuf, sizeof(sizeBuf), "%.1f KB", kb);
    }

    finding.detected = true;
    finding.riskLevel = 2;
    finding.ruleType = RULE_TYPE_OVERLAY;
    finding.title = "오버레이";
    finding.description =
        std::string("문서 끝에 ") + sizeBuf + " 크기의 추가 데이터 발견";
  }

  return finding;
}
