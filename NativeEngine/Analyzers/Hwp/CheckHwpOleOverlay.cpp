#include "CheckHwpOleOverlay.h"
#include <algorithm>
#include <cstring>
#include <vector>

DetectionFinding CheckHwpOleOverlay(FILE *fp, uint64_t fileSize,
                                    uint64_t &outLogicalSize) {
  DetectionFinding finding;
  finding.detected = false;

  if (fileSize < 512) {
    outLogicalSize = fileSize;
    return finding;
  }

  uint8_t header[512];
  if (_fseeki64(fp, 0, SEEK_SET) != 0 || fread(header, 1, 512, fp) < 512) {
    outLogicalSize = fileSize;
    return finding;
  }

  const uint8_t oleMagic[8] = {0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1};
  if (memcmp(header, oleMagic, 8) != 0) {
    outLogicalSize = fileSize;
    return finding;
  }

  uint16_t sectorShift = (header[30] | (header[31] << 8));
  uint32_t sectorSize = 1U << sectorShift;
  if (sectorSize < 512 || sectorSize > 4096)
    sectorSize = 512;

  uint32_t firstDifatSector = (header[68] | (header[69] << 8) |
                               (header[70] << 16) | (header[71] << 24));

  std::vector<uint32_t> fatSectorNums;
  for (int i = 0; i < 109; ++i) {
    uint32_t sec = (header[76 + i * 4] | (header[77 + i * 4] << 8) |
                    (header[78 + i * 4] << 16) | (header[79 + i * 4] << 24));
    if (sec < 0xFFFFFFFCU)
      fatSectorNums.push_back(sec);
  }

  uint32_t difatSec = firstDifatSector;
  int safety = 0;
  std::vector<uint32_t> visitedDifat;
  while (difatSec < 0xFFFFFFFCU && safety++ < 1000) {
    if (std::find(visitedDifat.begin(), visitedDifat.end(), difatSec) != visitedDifat.end())
      break;
    visitedDifat.push_back(difatSec);

    uint64_t off = 512ULL + (uint64_t)difatSec * sectorSize;
    if (off + sectorSize > fileSize)
      break;

    std::vector<uint8_t> buf(sectorSize);
    if (_fseeki64(fp, (long long)off, SEEK_SET) != 0)
      break;
    if (fread(buf.data(), 1, sectorSize, fp) < (size_t)sectorSize)
      break;

    int n = (int)(sectorSize / 4) - 1;
    for (int i = 0; i < n; ++i) {
      uint32_t s = (buf[i * 4] | (buf[i * 4 + 1] << 8) |
                    (buf[i * 4 + 2] << 16) | (buf[i * 4 + 3] << 24));
      if (s < 0xFFFFFFFCU)
        fatSectorNums.push_back(s);
    }
    difatSec = (buf[sectorSize - 4] | (buf[sectorSize - 3] << 8) |
                (buf[sectorSize - 2] << 16) | (buf[sectorSize - 1] << 24));
  }

  uint64_t maxUsedSectorIdx = 0;
  int entriesPerFatSec = (int)(sectorSize / 4);

  for (size_t fi = 0; fi < fatSectorNums.size() && fi < 10000; ++fi) {
    uint32_t fatSecNum = fatSectorNums[fi];
    uint64_t off = 512ULL + (uint64_t)fatSecNum * sectorSize;
    if (off + sectorSize > fileSize)
      continue;

    std::vector<uint8_t> buf(sectorSize);
    if (_fseeki64(fp, (long long)off, SEEK_SET) != 0)
      continue;
    if (fread(buf.data(), 1, sectorSize, fp) < (size_t)sectorSize)
      continue;

    for (int i = 0; i < entriesPerFatSec; ++i) {
      uint32_t entry = (buf[i * 4] | (buf[i * 4 + 1] << 8) |
                        (buf[i * 4 + 2] << 16) | (buf[i * 4 + 3] << 24));
      if (entry < 0xFFFFFFFCU) {
        uint64_t globalSecIdx = (uint64_t)fi * entriesPerFatSec + i;
        if (globalSecIdx > maxUsedSectorIdx)
          maxUsedSectorIdx = globalSecIdx;
      }
    }
  }

  if (fatSectorNums.empty()) {
    outLogicalSize = fileSize;
    return finding;
  }

  uint64_t logicalEnd = 512ULL + (maxUsedSectorIdx + 1) * (uint64_t)sectorSize;
  outLogicalSize = std::min(logicalEnd, fileSize);

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
    finding.riskLevel = 2; // Danger (Red)
    finding.ruleType = RULE_TYPE_OVERLAY;
    finding.title = "오버레이";
    finding.description =
        std::string("문서 끝에 ") + sizeBuf + " 크기의 추가 데이터 발견";
  }

  return finding;
}
