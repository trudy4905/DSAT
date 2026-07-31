#include "PdfDocumentAnalyzer.h"
#include <cstring>
#include <algorithm>

bool PdfDocumentAnalyzer::Analyze(FILE* fp, uint64_t fileSize, DocumentAnalysisResult& outResult) {
    strncpy_s(outResult.detectedFormat, sizeof(outResult.detectedFormat), "PDF", _TRUNCATE);

    // PDF 헤더 검증 (%PDF-)
    uint8_t pdfHeader[5];
    if (_fseeki64(fp, 0, SEEK_SET) != 0 || fread(pdfHeader, 1, 5, fp) < 5) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }
    if (memcmp(pdfHeader, "%PDF-", 5) != 0) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    // 검색 윈도우: 64KB (증분 저장 PDF 대응)
    size_t checkLen = static_cast<size_t>(std::min<uint64_t>(65536, fileSize));
    if (checkLen < 6) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    std::vector<uint8_t> buffer(checkLen);
    if (_fseeki64(fp, (long long)(fileSize - checkLen), SEEK_SET) != 0) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    size_t readBytes = fread(buffer.data(), 1, checkLen, fp);
    if (readBytes < 6) {
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    // %%EOF 역방향 탐색 (PDF 스펙: 퍼센트 2개 + EOF)
    // 증분 저장(Incremental Update) PDF는 %%EOF가 여러 개 있을 수 있음 → 마지막 것이 진짜 논리 끝
    int foundIdx = -1;
    for (int i = static_cast<int>(readBytes) - 6; i >= 0; --i) {
        if (buffer[i]   == '%' && buffer[i+1] == '%' &&
            buffer[i+2] == 'E' && buffer[i+3] == 'O' && buffer[i+4] == 'F') {
            foundIdx = i;
            break;
        }
    }

    if (foundIdx < 0) {
        // %%EOF 없음: 손상된 파일 또는 비표준 PDF → 정상(오탐 방지)
        FormatResult(fileSize, fileSize, outResult);
        return false;
    }

    // %%EOF 이후 허용 가능한 후행 바이트:
    // PDF 스펙 상 %%EOF 뒤에 최대 2개의 개행(\r, \n) 허용
    // 그 외 공백/탭도 관대하게 허용 (일부 생성기가 추가)
    uint64_t eofOffset = (fileSize - checkLen) + foundIdx + 5; // %%EOF = 5글자
    while (eofOffset < fileSize) {
        if (_fseeki64(fp, (long long)eofOffset, SEEK_SET) != 0) break;
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
