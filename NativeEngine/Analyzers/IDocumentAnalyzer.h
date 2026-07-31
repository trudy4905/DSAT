#ifndef I_DOCUMENT_ANALYZER_H
#define I_DOCUMENT_ANALYZER_H

#include "../Types.h"
#include <string>
#include <cstdio>

class IDocumentAnalyzer {
public:
    virtual ~IDocumentAnalyzer() = default;

    /* Returns format string, e.g. "HWP", "HWPX", "PDF" */
    virtual std::string GetFormatName() const = 0;

    /* Primary analysis function for overlay detection */
    virtual bool Analyze(FILE* fp, uint64_t fileSize, DocumentAnalysisResult& outResult) = 0;
};

#endif /* I_DOCUMENT_ANALYZER_H */
