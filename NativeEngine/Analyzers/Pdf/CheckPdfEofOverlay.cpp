#include "CheckPdfEofOverlay.h"
#include <cstring>
#include <algorithm>
#include <vector>
#include <cstdio>

DetectionFinding CheckPdfEofOverlay(FILE* fp, uint64_t fileSize, uint64_t& outLogicalSize) {
    DetectionFinding finding;
    finding.detected = false;

    // PDF 헤더 검증 (%PDF-)
    uint8_t pdfHeader[5];
    if (_fseeki64(fp, 0, SEEK_SET) != 0 || fread(pdfHeader, 1, 5, fp) < 5) {
        outLogicalSize = fileSize;
        return finding;
    }
    if (memcmp(pdfHeader, "%PDF-", 5) != 0) {
        outLogicalSize = fileSize;
        return finding;
    }

    size_t checkLen = static_cast<size_t>(std::min<uint64_t>(65536, fileSize));
    if (checkLen < 6) {
        outLogicalSize = fileSize;
        return finding;
    }

    std::vector<uint8_t> buffer(checkLen);
    if (_fseeki64(fp, (long long)(fileSize - checkLen), SEEK_SET) != 0) {
        outLogicalSize = fileSize;
        return finding;
    }

    size_t readBytes = fread(buffer.data(), 1, checkLen, fp);
    if (readBytes < 6) {
        outLogicalSize = fileSize;
        return finding;
    }

    int foundIdx = -1;
    for (int i = static_cast<int>(readBytes) - 5; i >= 0; --i) {
        if (buffer[i]   == '%' && buffer[i+1] == '%' &&
            buffer[i+2] == 'E' && buffer[i+3] == 'O' && buffer[i+4] == 'F') {
            foundIdx = i;
            break;
        }
    }

    if (foundIdx < 0) {
        outLogicalSize = fileSize;
        return finding;
    }

    size_t padIdx = foundIdx + 5;
    while (padIdx < readBytes) {
        uint8_t ch = buffer[padIdx];
        if (ch == '\r' || ch == '\n' || ch == ' ' || ch == '\t' || ch == '\0') {
            padIdx++;
        } else {
            break;
        }
    }

    outLogicalSize = (fileSize - checkLen) + padIdx;

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
        finding.title = "PDF EOF 오버레이 탐지";
        finding.description = std::string("%%EOF 이후 ") + sizeBuf + " 크기의 오버레이 데이터 발견";
    }

    return finding;
}
