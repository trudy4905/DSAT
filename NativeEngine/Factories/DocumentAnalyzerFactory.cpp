#include "DocumentAnalyzerFactory.h"
#include "../Analyzers/HwpDocumentAnalyzer.h"
#include "../Analyzers/HwpxDocumentAnalyzer.h"
#include "../Analyzers/PdfDocumentAnalyzer.h"
#include <algorithm>
#include <cctype>

std::unique_ptr<IDocumentAnalyzer> DocumentAnalyzerFactory::CreateAnalyzer(const std::wstring& filePath) {
    size_t dotIdx = filePath.rfind(L'.');
    std::wstring ext = L"";
    if (dotIdx != std::wstring::npos) {
        ext = filePath.substr(dotIdx);
        std::transform(ext.begin(), ext.end(), ext.begin(), [](wchar_t c) -> wchar_t {
            return static_cast<wchar_t>(::towlower(c));
        });
    }

    if (ext == L".hwp") {
        return std::make_unique<HwpDocumentAnalyzer>();
    }
    else if (ext == L".hwpx") {
        return std::make_unique<HwpxDocumentAnalyzer>();
    }
    else if (ext == L".pdf") {
        return std::make_unique<PdfDocumentAnalyzer>();
    }

    return nullptr;
}
