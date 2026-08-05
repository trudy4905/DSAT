#include "HwpxDocumentAnalyzer.h"
#include "Hwpx/CheckHwpxZipOverlay.h"
#include <cstring>
#include <vector>

bool HwpxDocumentAnalyzer::Analyze(FILE* fp, uint64_t fileSize, DocumentAnalysisResult& outResult) {
    strncpy_s(outResult.detectedFormat, sizeof(outResult.detectedFormat), "HWPX", _TRUNCATE);

    std::vector<DetectionFinding> findings;
    uint64_t logicalSize = fileSize;

    DetectionFinding f1 = CheckHwpxZipOverlay(fp, fileSize, logicalSize);
    if (f1.detected) {
        findings.push_back(f1);
    }

    FormatResult(fileSize, logicalSize, findings, outResult);
    return true;
}
