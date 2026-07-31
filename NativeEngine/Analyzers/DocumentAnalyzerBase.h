#ifndef DOCUMENT_ANALYZER_BASE_H
#define DOCUMENT_ANALYZER_BASE_H

#include "IDocumentAnalyzer.h"
#include <vector>

class DocumentAnalyzerBase : public IDocumentAnalyzer {
protected:
    static uint16_t ReadU16(const uint8_t* p);
    static uint32_t ReadU32(const uint8_t* p);
    static bool MatchBytes(const uint8_t* buffer, size_t bufferLen, size_t offset, const uint8_t* target, size_t targetLen);
    
    /* Format overlay status message into outResult */
    void FormatResult(uint64_t physicalSize, uint64_t logicalSize, DocumentAnalysisResult& outResult);
};

#endif /* DOCUMENT_ANALYZER_BASE_H */
