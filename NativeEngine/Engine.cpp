#ifndef ENGINE_EXPORTS
#define ENGINE_EXPORTS
#endif

#include "Engine.h"
#include "Managers/EngineStatusManager.h"
#include "Factories/DocumentAnalyzerFactory.h"
#include <cstring>
#include <cstdio>
#include <memory>
#include <string>

ENGINE_API int32_t __cdecl Engine_Initialize(void) {
    return EngineStatusManager::GetInstance().Initialize() ? 1 : 0;
}

ENGINE_API void __cdecl Engine_Shutdown(void) {
    EngineStatusManager::GetInstance().Shutdown();
}

ENGINE_API void __cdecl Engine_GetStatus(EngineStatusInfo *outStatus) {
    EngineStatusManager::GetInstance().GetStatus(outStatus);
}

ENGINE_API void __cdecl Engine_SetProgressCallback(EngineProgressCallback callback) {
    EngineStatusManager::GetInstance().SetProgressCallback(callback);
}

ENGINE_API void __cdecl Engine_SetLogCallback(EngineLogCallback callback) {
    EngineStatusManager::GetInstance().SetLogCallback(callback);
}

ENGINE_API int32_t __cdecl Engine_ProcessDataArray(const double *inputData, double *outputData, int32_t dataLength, double multiplier) {
    if (!inputData || !outputData || dataLength <= 0) return 0;
    for (int i = 0; i < dataLength; ++i) {
        outputData[i] = inputData[i] * multiplier;
    }
    return 1;
}

ENGINE_API int32_t __cdecl Engine_RunAsyncSimulation(int32_t totalSteps, int32_t stepDelayMs) {
    (void)totalSteps;
    (void)stepDelayMs;
    return 1;
}

ENGINE_API int32_t __cdecl Engine_ProcessString(const char *inputStr, char *outputBuffer, int32_t bufferSize) {
    if (!inputStr || !outputBuffer || bufferSize <= 0) return 0;
    std::string processed = "[C++ Engine OOP Facade]: " + std::string(inputStr);
    strncpy_s(outputBuffer, bufferSize, processed.c_str(), _TRUNCATE);
    return 1;
}

/* Document Structure & EOF Overlay Analysis API Facade */
ENGINE_API int32_t __cdecl Engine_AnalyzeDocumentOverlay(const char* filePath, DocumentAnalysisResult* outResult) {
    if (!filePath || !outResult) return 0;

    memset(outResult, 0, sizeof(DocumentAnalysisResult));
    outResult->isNormal = 1;
    outResult->hasOverlay = 0;

    auto analyzer = DocumentAnalyzerFactory::CreateAnalyzer(filePath);
    if (!analyzer) {
        strncpy_s(outResult->detectedFormat, sizeof(outResult->detectedFormat), "UNKNOWN", _TRUNCATE);
        strncpy_s(outResult->statusMessage, sizeof(outResult->statusMessage), "지원하지 않는 확장자", _TRUNCATE);
        return 1;
    }

    FILE* fp = nullptr;
    if (fopen_s(&fp, filePath, "rb") != 0 || !fp) {
        strncpy_s(outResult->statusMessage, sizeof(outResult->statusMessage), "파일 열기 실패", _TRUNCATE);
        outResult->isNormal = 0;
        return 0;
    }

    _fseeki64(fp, 0, SEEK_END);
    uint64_t physicalSize = _ftelli64(fp);
    outResult->physicalSize = physicalSize;

    if (physicalSize == 0) {
        fclose(fp);
        strncpy_s(outResult->statusMessage, sizeof(outResult->statusMessage), "빈 파일 (0 B)", _TRUNCATE);
        return 1;
    }

    bool success = analyzer->Analyze(fp, physicalSize, *outResult);
    fclose(fp);

    return success ? 1 : 0;
}
