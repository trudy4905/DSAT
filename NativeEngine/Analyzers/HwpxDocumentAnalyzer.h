#ifndef HWPX_DOCUMENT_ANALYZER_H
#define HWPX_DOCUMENT_ANALYZER_H

#include "DocumentAnalyzerBase.h"

class HwpxDocumentAnalyzer : public DocumentAnalyzerBase {
public:
    std::string GetFormatName() const override { return "HWPX"; }
    bool Analyze(FILE* fp, uint64_t fileSize, DocumentAnalysisResult& outResult) override;
};

#endif /* HWPX_DOCUMENT_ANALYZER_H */
