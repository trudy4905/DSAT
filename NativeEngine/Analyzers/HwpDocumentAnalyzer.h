#ifndef HWP_DOCUMENT_ANALYZER_H
#define HWP_DOCUMENT_ANALYZER_H

#include "DocumentAnalyzerBase.h"

class HwpDocumentAnalyzer : public DocumentAnalyzerBase {
public:
    std::string GetFormatName() const override { return "HWP"; }
    bool Analyze(FILE* fp, uint64_t fileSize, DocumentAnalysisResult& outResult) override;
};

#endif /* HWP_DOCUMENT_ANALYZER_H */
