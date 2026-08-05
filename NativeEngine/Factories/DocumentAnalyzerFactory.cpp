#include "DocumentAnalyzerFactory.h"
#include "../Analyzers/HwpDocumentAnalyzer.h"
#include "../Analyzers/HwpxDocumentAnalyzer.h"
#include "../Analyzers/PdfDocumentAnalyzer.h"
#include <algorithm>
#include <cctype>

std::unique_ptr<IDocumentAnalyzer> DocumentAnalyzerFactory::CreateAnalyzer(const std::string& filePath) {
    size_t dotIdx = filePath.rfind('.');
    std::string ext = "";
    if (dotIdx != std::string::npos) {
        ext = filePath.substr(dotIdx);
        std::transform(ext.begin(), ext.end(), ext.begin(), [](unsigned char c) -> char {
            return static_cast<char>(::tolower(c));
        });
    }

    if (ext == ".hwp") {
        return std::make_unique<HwpDocumentAnalyzer>();
    }
    else if (ext == ".hwpx") {
        return std::make_unique<HwpxDocumentAnalyzer>();
    }
    else if (ext == ".pdf") {
        return std::make_unique<PdfDocumentAnalyzer>();
    }

    return nullptr;
}
