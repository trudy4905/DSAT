#ifndef TYPES_H
#define TYPES_H

#include <stdint.h>

#pragma pack(push, 8)
typedef struct {
    int32_t isRunning;
    int32_t coreCount;
    double lastExecutionTimeMs;
    uint64_t totalProcessedItems;
} EngineStatusInfo;

typedef enum {
    RULE_TYPE_NONE = 0,
    RULE_TYPE_OVERLAY = 1,
    RULE_TYPE_MACRO_SCRIPT = 2,
    RULE_TYPE_STRUCTURE_ANOMALY = 3,
    RULE_TYPE_ENCRYPTED_STREAM = 4
} DetectionRuleType;

typedef struct {
    int32_t riskLevel;          /* 0 = Safe, 1 = Caution, 2 = Danger, 3 = Critical */
    int32_t ruleType;           /* DetectionRuleType enum */
    char title[64];             /* 탐지 항목 제목 (예: "데이터 오버레이 탐지") */
    char description[256];      /* 탐지 항목 상세 설명 */
} NativeFindingItem;

typedef struct {
    int32_t isNormal;           /* 1 = Normal, 0 = Anomaly */
    int32_t hasOverlay;         /* 1 = Overlay Detected, 0 = None */
    int32_t riskLevel;          /* 0 = Safe, 1 = Caution, 2 = Danger, 3 = Critical */
    int32_t findingCount;       /* Total detected anomaly count */
    uint64_t logicalSize;       /* Calculated logical document size (bytes) */
    uint64_t physicalSize;      /* Physical file size (bytes) */
    uint64_t overlaySize;       /* Overlay size (bytes) */
    char detectedFormat[16];    /* "HWP", "HWPX", "PDF", "UNKNOWN" */
    char statusMessage[128];    /* Summary status message string */
    NativeFindingItem findings[8]; /* Detailed findings array (Max 8) */
} DocumentAnalysisResult;
#pragma pack(pop)

#endif /* TYPES_H */
