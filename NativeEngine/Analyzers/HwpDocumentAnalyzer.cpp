#include "HwpDocumentAnalyzer.h"
#include <cstring>
#include <algorithm>
#include <vector>

bool HwpDocumentAnalyzer::Analyze(FILE* fp, uint64_t fileSize, DocumentAnalysisResult& outResult) {
    strncpy_s(outResult.detectedFormat, sizeof(outResult.detectedFormat), "HWP", _TRUNCATE);

    if (fileSize < 512) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    uint8_t header[512];
    if (_fseeki64(fp, 0, SEEK_SET) != 0 || fread(header, 1, 512, fp) < 512) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    const uint8_t oleMagic[8] = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
    if (memcmp(header, oleMagic, 8) != 0) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    uint16_t sectorShift = ReadU16(&header[30]);
    uint32_t sectorSize = 1U << sectorShift;
    if (sectorSize < 512 || sectorSize > 4096) sectorSize = 512;

    uint32_t firstDifatSector = ReadU32(&header[68]);

    // Step 1: 모든 FAT 섹터 번호 수집 (헤더 DIFAT 109개 + Extension 체인)
    std::vector<uint32_t> fatSectorNums;

    // 헤더 내 109개 DIFAT 엔트리
    for (int i = 0; i < 109; ++i) {
        uint32_t sec = ReadU32(&header[76 + i * 4]);
        if (sec < 0xFFFFFFFCU) fatSectorNums.push_back(sec);
    }

    // DIFAT Extension 체인 추적 (109섹터 초과 대형 파일 대응)
    uint32_t difatSec = firstDifatSector;
    int safety = 0;
    while (difatSec < 0xFFFFFFFCU && safety++ < 1000) {
        uint64_t off = 512ULL + (uint64_t)difatSec * sectorSize;
        if (off + sectorSize > fileSize) break;

        std::vector<uint8_t> buf(sectorSize);
        if (_fseeki64(fp, (long long)off, SEEK_SET) != 0) break;
        if (fread(buf.data(), 1, sectorSize, fp) < (size_t)sectorSize) break;

        // 마지막 4바이트는 다음 DIFAT 섹터 포인터, 나머지는 FAT 섹터 번호
        int n = (int)(sectorSize / 4) - 1;
        for (int i = 0; i < n; ++i) {
            uint32_t s = ReadU32(buf.data() + i * 4);
            if (s < 0xFFFFFFFCU) fatSectorNums.push_back(s);
        }
        difatSec = ReadU32(buf.data() + sectorSize - 4);
    }

    // Step 2: FAT 전체 읽기 → 사용 중인 마지막 섹터 인덱스 탐색
    // FREESECT = 0xFFFFFFFF (미사용), 나머지(FATSECT, DIFSECT, ENDOFCHAIN, 체인 링크)는 모두 사용 중
    uint64_t maxUsedSectorIdx = 0;
    int entriesPerFatSec = (int)(sectorSize / 4);

    for (size_t fi = 0; fi < fatSectorNums.size(); ++fi) {
        uint32_t fatSecNum = fatSectorNums[fi];
        uint64_t off = 512ULL + (uint64_t)fatSecNum * sectorSize;
        if (off + sectorSize > fileSize) continue;

        std::vector<uint8_t> buf(sectorSize);
        if (_fseeki64(fp, (long long)off, SEEK_SET) != 0) continue;
        if (fread(buf.data(), 1, sectorSize, fp) < (size_t)sectorSize) continue;

        for (int i = 0; i < entriesPerFatSec; ++i) {
            uint32_t entry = ReadU32(buf.data() + i * 4);
            // FREESECT(0xFFFFFFFF)가 아닌 모든 섹터는 OLE 컨테이너가 점유 중
            if (entry != 0xFFFFFFFFU) {
                uint64_t globalSecIdx = (uint64_t)fi * entriesPerFatSec + i;
                if (globalSecIdx > maxUsedSectorIdx) maxUsedSectorIdx = globalSecIdx;
            }
        }
    }

    // FAT 배열이 전혀 없으면 폴백: 파일 전체를 논리 크기로 간주
    if (fatSectorNums.empty()) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    uint64_t logicalEnd = 512ULL + (maxUsedSectorIdx + 1) * (uint64_t)sectorSize;
    uint64_t logicalSize = std::min(logicalEnd, fileSize);

    FormatResult(fileSize, logicalSize, outResult);
    return true;
}
