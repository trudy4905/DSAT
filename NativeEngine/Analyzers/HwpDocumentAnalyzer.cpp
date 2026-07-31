#include "HwpDocumentAnalyzer.h"
#include <cstring>
#include <algorithm>

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

    uint32_t dirStartSector = ReadU32(&header[48]);
    uint64_t maxSectorIndex = 0;

    if (dirStartSector != 0xFFFFFFFEU && dirStartSector != 0xFFFFFFFFU) {
        if (dirStartSector > maxSectorIndex) maxSectorIndex = dirStartSector;
    }

    for (int i = 0; i < 109; ++i) {
        uint32_t satSec = ReadU32(&header[76 + i * 4]);
        if (satSec != 0xFFFFFFFEU && satSec != 0xFFFFFFFFU) {
            if (satSec > maxSectorIndex) maxSectorIndex = satSec;
        }
    }

    uint64_t logicalOleEnd = 512 + (maxSectorIndex + 1) * sectorSize;
    uint64_t logicalSize = std::min<uint64_t>(logicalOleEnd, fileSize);

    FormatResult(fileSize, logicalSize, outResult);
    return true;
}
