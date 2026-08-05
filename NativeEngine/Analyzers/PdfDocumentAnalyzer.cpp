#include "PdfDocumentAnalyzer.h"
#include "Pdf/CheckPdfEofOverlay.h"
#include <cstring>
#include <vector>

bool PdfDocumentAnalyzer::Analyze(FILE* fp, uint64_t fileSize, DocumentAnalysisResult& outResult) {
    strncpy_s(outResult.detectedFormat, sizeof(outResult.detectedFormat), "PDF", _TRUNCATE);

    std::vector<DetectionFinding> findings;
    uint64_t logicalSize = fileSize;

    DetectionFinding f1 = CheckPdfEofOverlay(fp, fileSize, logicalSize);
    if (f1.detected) {
        findings.push_back(f1);
    }

    FormatResult(fileSize, logicalSize, findings, outResult);
    return true;
}
