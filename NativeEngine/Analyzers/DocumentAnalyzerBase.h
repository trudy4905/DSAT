#ifndef DOCUMENT_ANALYZER_BASE_H
#define DOCUMENT_ANALYZER_BASE_H

#include "IDocumentAnalyzer.h"
#include <vector>
#include <string>

struct DetectionFinding {
    bool detected = false;
    int riskLevel = 0;                  // 0: Safe, 1: Caution, 2: Danger, 3: Critical
    DetectionRuleType ruleType = RULE_TYPE_NONE; // 탐지 기법 열거형
    std::string title;                   // UI 탐지 항목 제목 (예: "데이터 오버레이 탐지")
    std::string description;             // 상세 메시지
};

class DocumentAnalyzerBase : public IDocumentAnalyzer {
protected:
    static uint16_t ReadU16(const uint8_t* p);
    static uint32_t ReadU32(const uint8_t* p);
    static bool MatchBytes(const uint8_t* buffer, size_t bufferLen, size_t offset, const uint8_t* target, size_t targetLen);
    
    /* Format findings & overlay status into outResult */
    void FormatResult(uint64_t physicalSize, uint64_t logicalSize, const std::vector<DetectionFinding>& findings, DocumentAnalysisResult& outResult);
};

#endif /* DOCUMENT_ANALYZER_BASE_H */
