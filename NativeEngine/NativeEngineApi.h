#ifndef NATIVE_ENGINE_API_H
#define NATIVE_ENGINE_API_H

#include <stdint.h>
#include <stdbool.h>

#ifdef NATIVEENGINE_EXPORTS
    #define NATIVE_API __declspec(dllexport)
#else
    #define NATIVE_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

#pragma pack(push, 8)

/* -------------------------------------------------------------------------
 * Engine Telemetry & LifeCycle Structs
 * ------------------------------------------------------------------------- */
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
    char title[64];             /* 탐지 항목 제목 */
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

/* -------------------------------------------------------------------------
 * Forensic Disk Image Inspection Structs
 * ------------------------------------------------------------------------- */
typedef struct {
    int partitionIndex;
    int sectorSize;
    uint64_t startSector;
    uint64_t sectorCount;
    char filesystem[32];
    bool isSupported;
} PartitionItemInfo;

typedef struct {
    bool isValid;
    char imageTypeTag[64];
    uint64_t totalImageSize;
    uint64_t totalPartitionSize;
    int partitionCount;
    PartitionItemInfo partitions[16];
    char errorMessage[256];
} ImageInspectionOutput;

#pragma pack(pop)

/* -------------------------------------------------------------------------
 * Callback Delegates
 * ------------------------------------------------------------------------- */
typedef void (__stdcall *EngineProgressCallback)(int32_t progressPercent, const char* statusMessage);
typedef void (__stdcall *EngineLogCallback)(int32_t logLevel, const char* logMessage);
typedef void (__stdcall *ImageScanProgressCallback)(int scannedCount, const wchar_t* currentPath, const wchar_t* statusMsg);

/* -------------------------------------------------------------------------
 * Unified C-API Facade Export Functions (__stdcall)
 * ------------------------------------------------------------------------- */

/* 1. LifeCycle & Status Control */
NATIVE_API int32_t __stdcall Engine_Initialize(void);
NATIVE_API void __stdcall Engine_Shutdown(void);
NATIVE_API void __stdcall Engine_GetStatus(EngineStatusInfo* outStatus);
NATIVE_API void __stdcall Engine_SetProgressCallback(EngineProgressCallback callback);
NATIVE_API void __stdcall Engine_SetLogCallback(EngineLogCallback callback);

/* 2. Numerical / Interop Array Test */
NATIVE_API int32_t __stdcall Engine_ProcessDataArray(const double* inputData, double* outputData, int32_t dataLength, double multiplier);

/* 3. Single Document Overlay & Anomaly Analysis */
NATIVE_API int32_t __stdcall Engine_AnalyzeDocumentOverlay(const wchar_t* filePath, DocumentAnalysisResult* outResult);

/* 4. Forensic Disk Image Inspection (.E01 / .DD / .RAW) */
NATIVE_API int32_t __stdcall Engine_InspectForensicImage(const wchar_t* imagePath, ImageInspectionOutput* outResult);

/* 5. Extract Document Files (.hwp, .hwpx, .pdf) from Forensic Disk Image */
NATIVE_API int32_t __stdcall Engine_ExtractDocumentFilesFromImage(
    const wchar_t* imagePath,
    const wchar_t* tempExtractDir,
    int32_t includeDeleted,
    ImageScanProgressCallback callback,
    int32_t* outExtractedCount
);

#ifdef __cplusplus
}
#endif

#endif /* NATIVE_ENGINE_API_H */
