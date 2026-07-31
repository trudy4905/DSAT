#include "HwpxDocumentAnalyzer.h"
#include <cstring>
#include <algorithm>

bool HwpxDocumentAnalyzer::Analyze(FILE* fp, uint64_t fileSize, DocumentAnalysisResult& outResult) {
    strncpy_s(outResult.detectedFormat, sizeof(outResult.detectedFormat), "HWPX", _TRUNCATE);

    size_t checkLen = static_cast<size_t>(std::min<uint64_t>(65557, fileSize));
    if (checkLen < 22) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    std::vector<uint8_t> buffer(checkLen);
    if (_fseeki64(fp, fileSize - checkLen, SEEK_SET) != 0) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    size_t readBytes = fread(buffer.data(), 1, checkLen, fp);
    if (readBytes < 22) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    int foundIdx = -1;
    for (int i = static_cast<int>(readBytes) - 22; i >= 0; --i) {
        if (buffer[i] == 0x50 && buffer[i+1] == 0x4B && buffer[i+2] == 0x05 && buffer[i+3] == 0x06) {
            foundIdx = i;
            break;
        }
    }

    if (foundIdx < 0) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    uint16_t commentLen = ReadU16(&buffer[foundIdx + 20]);
    uint64_t eocdEnd = (fileSize - checkLen) + foundIdx + 22 + commentLen;
    uint64_t logicalSize = std::min<uint64_t>(eocdEnd, fileSize);

    FormatResult(fileSize, logicalSize, outResult);
    return true;
}
