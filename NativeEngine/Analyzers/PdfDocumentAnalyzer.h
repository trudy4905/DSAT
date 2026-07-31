#ifndef PDF_DOCUMENT_ANALYZER_H
#define PDF_DOCUMENT_ANALYZER_H

#include "DocumentAnalyzerBase.h"

class PdfDocumentAnalyzer : public DocumentAnalyzerBase {
public:
    std::string GetFormatName() const override { return "PDF"; }
    bool Analyze(FILE* fp, uint64_t fileSize, DocumentAnalysisResult& outResult) override;
};

#endif /* PDF_DOCUMENT_ANALYZER_H */
