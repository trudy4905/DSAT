#ifndef DOCUMENT_ANALYZER_FACTORY_H
#define DOCUMENT_ANALYZER_FACTORY_H

#include "../Analyzers/IDocumentAnalyzer.h"
#include <memory>
#include <string>

class DocumentAnalyzerFactory {
public:
    static std::unique_ptr<IDocumentAnalyzer> CreateAnalyzer(const std::wstring& filePath);
};

#endif /* DOCUMENT_ANALYZER_FACTORY_H */
