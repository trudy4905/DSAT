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
} DocumentAnalysisResult;
#pragma pack(pop)

#endif /* TYPES_H */
