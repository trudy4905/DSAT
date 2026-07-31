#ifndef ENGINE_H
#define ENGINE_H

#include "Types.h"
#include <windows.h>

#ifdef ENGINE_EXPORTS
    #define ENGINE_API __declspec(dllexport)
#else
    #define ENGINE_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef void (__cdecl *EngineProgressCallback)(int32_t progressPercent, const char* statusMessage);
typedef void (__cdecl *EngineLogCallback)(int32_t logLevel, const char* logMessage);

ENGINE_API int32_t __cdecl Engine_Initialize(void);
ENGINE_API void __cdecl Engine_Shutdown(void);
ENGINE_API void __cdecl Engine_GetStatus(EngineStatusInfo* outStatus);
ENGINE_API void __cdecl Engine_SetProgressCallback(EngineProgressCallback callback);
ENGINE_API void __cdecl Engine_SetLogCallback(EngineLogCallback callback);
ENGINE_API int32_t __cdecl Engine_ProcessDataArray(const double* inputData, double* outputData, int32_t dataLength, double multiplier);
ENGINE_API int32_t __cdecl Engine_RunAsyncSimulation(int32_t totalSteps, int32_t stepDelayMs);
ENGINE_API int32_t __cdecl Engine_ProcessString(const char* inputStr, char* outputBuffer, int32_t bufferSize);

/* Document Structure & EOF Overlay Analysis API */
ENGINE_API int32_t __cdecl Engine_AnalyzeDocumentOverlay(const char* filePath, DocumentAnalysisResult* outResult);

#ifdef __cplusplus
}
#endif

#endif /* ENGINE_H */
