#include "CheckHwpOleSlack.h"
#include <algorithm>
#include <cstring>
#include <vector>
#include <string>

DetectionFinding CheckHwpOleSlack(FILE *fp, uint64_t fileSize) {
  DetectionFinding finding;
  finding.detected = false;

  if (fileSize < 512) {
    return finding;
  }

  uint8_t header[512];
  if (_fseeki64(fp, 0, SEEK_SET) != 0 || fread(header, 1, 512, fp) < 512) {
    return finding;
  }

  const uint8_t oleMagic[8] = {0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1};
  if (memcmp(header, oleMagic, 8) != 0) {
    return finding;
  }

  uint16_t sectorShift = (header[30] | (header[31] << 8));
  uint32_t sectorSize = 1U << sectorShift;
  if (sectorSize < 512 || sectorSize > 4096)
    sectorSize = 512;

  uint32_t firstDirSector = (header[48] | (header[49] << 8) |
                            (header[50] << 16) | (header[51] << 24));
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
    if (_fseeki64(fp, (long long)off, SEEK_SET) != 0 ||
        fread(buf.data(), 1, sectorSize, fp) < (size_t)sectorSize)
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

  if (fatSectorNums.empty()) {
    return finding;
  }

  // Load FAT table
  int entriesPerFatSec = (int)(sectorSize / 4);
  std::vector<uint32_t> fatTable;
  fatTable.resize(fatSectorNums.size() * entriesPerFatSec, 0xFFFFFFFFU);

  for (size_t fi = 0; fi < fatSectorNums.size() && fi < 10000; ++fi) {
    uint32_t fatSecNum = fatSectorNums[fi];
    uint64_t off = 512ULL + (uint64_t)fatSecNum * sectorSize;
    if (off + sectorSize > fileSize)
      continue;

    std::vector<uint8_t> buf(sectorSize);
    if (_fseeki64(fp, (long long)off, SEEK_SET) != 0 ||
        fread(buf.data(), 1, sectorSize, fp) < (size_t)sectorSize)
      continue;

    for (int i = 0; i < entriesPerFatSec; ++i) {
      uint32_t entry = (buf[i * 4] | (buf[i * 4 + 1] << 8) |
                        (buf[i * 4 + 2] << 16) | (buf[i * 4 + 3] << 24));
      size_t globalIdx = fi * entriesPerFatSec + i;
      if (globalIdx < fatTable.size()) {
        fatTable[globalIdx] = entry;
      }
    }
  }

  // Follow Directory Stream sector chain
  std::vector<uint8_t> dirData;
  uint32_t currDirSec = firstDirSector;
  int dirSafety = 0;
  while (currDirSec < 0xFFFFFFFCU && dirSafety++ < 2000) {
    uint64_t off = 512ULL + (uint64_t)currDirSec * sectorSize;
    if (off + sectorSize > fileSize)
      break;

    std::vector<uint8_t> buf(sectorSize);
    if (_fseeki64(fp, (long long)off, SEEK_SET) != 0 ||
        fread(buf.data(), 1, sectorSize, fp) < (size_t)sectorSize)
      break;

    dirData.insert(dirData.end(), buf.begin(), buf.end());

    if (currDirSec < fatTable.size()) {
      currDirSec = fatTable[currDirSec];
    } else {
      break;
    }
  }

  if (dirData.size() < 128) {
    return finding;
  }

  uint64_t totalSlackStegoBytes = 0;
  size_t entryCount = dirData.size() / 128;

  for (size_t e = 0; e < entryCount; ++e) {
    const uint8_t *entry = dirData.data() + e * 128;
    uint8_t objectType = entry[66];
    if (objectType != 2) {
      // Process Type 2 = Standard Stream only (exclude Type 5 Root Storage MiniStream container)
      continue;
    }

    uint32_t startSec = (entry[116] | (entry[117] << 8) |
                         (entry[118] << 16) | (entry[119] << 24));
    uint64_t streamSize = (entry[120] | (entry[121] << 8) |
                           (entry[122] << 16) | (entry[123] << 24) |
                           ((uint64_t)entry[124] << 32) | ((uint64_t)entry[125] << 40) |
                           ((uint64_t)entry[126] << 48) | ((uint64_t)entry[127] << 56));

    // Standard FAT stream (>= 4096 bytes)
    if (streamSize < 4096 || startSec >= 0xFFFFFFFCU) {
      continue;
    }

    // Follow stream sector chain to find the last sector
    uint32_t lastSec = startSec;
    uint32_t currSec = startSec;
    uint64_t sectorCount = 0;
    std::vector<uint32_t> visitedChain;

    while (currSec < 0xFFFFFFFCU && sectorCount < 100000) {
      if (std::find(visitedChain.begin(), visitedChain.end(), currSec) != visitedChain.end()) {
        break; // Cycle detected
      }
      visitedChain.push_back(currSec);
      lastSec = currSec;
      sectorCount++;
      if (currSec < fatTable.size()) {
        currSec = fatTable[currSec];
      } else {
        break;
      }
    }

    if (sectorCount == 0) continue;

    uint64_t allocatedBytes = sectorCount * (uint64_t)sectorSize;
    if (allocatedBytes > streamSize) {
      uint64_t bytesInLastSector = streamSize % (uint64_t)sectorSize;
      if (bytesInLastSector > 0) {
        uint64_t slackLen = (uint64_t)sectorSize - bytesInLastSector;
        uint64_t lastSectorOffset = 512ULL + (uint64_t)lastSec * sectorSize;
        uint64_t slackOffset = lastSectorOffset + bytesInLastSector;

        if (slackOffset + slackLen <= fileSize) {
          std::vector<uint8_t> slackBuf((size_t)slackLen);
          if (_fseeki64(fp, (long long)slackOffset, SEEK_SET) == 0 &&
              fread(slackBuf.data(), 1, (size_t)slackLen, fp) == (size_t)slackLen) {
            for (uint8_t b : slackBuf) {
              if (b != 0x00 && b != 0xFF) {
                totalSlackStegoBytes++;
              }
            }
          }
        }
      }
    }
  }

  const uint64_t MIN_SLACK_STEGO_THRESHOLD = 256;
  if (totalSlackStegoBytes >= MIN_SLACK_STEGO_THRESHOLD) {
    double kb = totalSlackStegoBytes / 1024.0;
    char sizeBuf[64];
    if (kb >= 1024.0) {
      sprintf_s(sizeBuf, sizeof(sizeBuf), "%.2f MB", kb / 1024.0);
    } else {
      sprintf_s(sizeBuf, sizeof(sizeBuf), "%.1f KB", kb);
    }

    finding.detected = true;
    finding.riskLevel = 2; // Danger (Orange-Red)
    finding.ruleType = RULE_TYPE_OLE_SLACK;
    finding.title = "OLE 슬랙 은닉";
    finding.description =
        std::string("OLE 컨테이너 슬랙 영역에 ") + sizeBuf + " 크기의 은닉 데이터 발견";
  }

  return finding;
}
