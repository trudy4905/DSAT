#include "PdfDocumentAnalyzer.h"
#include <cstring>
#include <algorithm>

bool PdfDocumentAnalyzer::Analyze(FILE* fp, uint64_t fileSize, DocumentAnalysisResult& outResult) {
    strncpy_s(outResult.detectedFormat, sizeof(outResult.detectedFormat), "PDF", _TRUNCATE);

    size_t checkLen = static_cast<size_t>(std::min<uint64_t>(16384, fileSize));
    if (checkLen < 5) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    std::vector<uint8_t> buffer(checkLen);
    if (_fseeki64(fp, fileSize - checkLen, SEEK_SET) != 0) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    size_t readBytes = fread(buffer.data(), 1, checkLen, fp);
    if (readBytes < 5) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    int foundIdx = -1;
    for (int i = static_cast<int>(readBytes) - 5; i >= 0; --i) {
        if (buffer[i] == '%' && buffer[i+1] == 'E' && buffer[i+2] == 'O' && buffer[i+3] == 'F') {
            foundIdx = i;
            break;
        }
    }

    if (foundIdx < 0) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    uint64_t eofOffset = (fileSize - checkLen) + foundIdx + 4;
    while (eofOffset < fileSize) {
        if (_fseeki64(fp, eofOffset, SEEK_SET) != 0) break;
        int ch = fgetc(fp);
        if (ch == '\r' || ch == '\n' || ch == ' ' || ch == '\t') {
            eofOffset++;
        } else {
            break;
        }
    }

    FormatResult(fileSize, eofOffset, outResult);
    return true;
}
