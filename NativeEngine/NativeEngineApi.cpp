#include "NativeEngineApi.h"
#include "Managers/EngineStatusManager.h"
#include "Factories/DocumentAnalyzerFactory.h"
#include "ImageReaders/ImageReaderFactory.h"
#include "FileSystem/TskFileSystemModule.h"
#include <windows.h>
#include <cstdio>
#include <cstring>
#include <memory>
#include <string>



extern "C" {

/* -------------------------------------------------------------------------
 * 1. LifeCycle & Status Control
 * ------------------------------------------------------------------------- */
NATIVE_API int32_t __stdcall Engine_Initialize(void) {
    return EngineStatusManager::GetInstance().Initialize() ? 1 : 0;
}

NATIVE_API void __stdcall Engine_Shutdown(void) {
    EngineStatusManager::GetInstance().Shutdown();
}

NATIVE_API void __stdcall Engine_GetStatus(EngineStatusInfo* outStatus) {
    EngineStatusManager::GetInstance().GetStatus(outStatus);
}

NATIVE_API void __stdcall Engine_SetProgressCallback(EngineProgressCallback callback) {
    EngineStatusManager::GetInstance().SetProgressCallback(callback);
}

NATIVE_API void __stdcall Engine_SetLogCallback(EngineLogCallback callback) {
    EngineStatusManager::GetInstance().SetLogCallback(callback);
}

/* -------------------------------------------------------------------------
 * 2. Interop Array Data Processing
 * ------------------------------------------------------------------------- */
NATIVE_API int32_t __stdcall Engine_ProcessDataArray(const double* inputData, double* outputData, int32_t dataLength, double multiplier) {
    if (!inputData || !outputData || dataLength <= 0) return 0;
    for (int32_t i = 0; i < dataLength; ++i) {
        outputData[i] = inputData[i] * multiplier;
    }
    return 1;
}

/* -------------------------------------------------------------------------
 * 3. Single Document Overlay & Anomaly Analysis
 * ------------------------------------------------------------------------- */
/* -------------------------------------------------------------------------
 * 3. Single Document Overlay & Anomaly Analysis
 * ------------------------------------------------------------------------- */
NATIVE_API int32_t __stdcall Engine_AnalyzeDocumentOverlay(const wchar_t* filePath, DocumentAnalysisResult* outResult) {
    if (!filePath || !outResult) return 0;

    memset(outResult, 0, sizeof(DocumentAnalysisResult));
    outResult->isNormal = 1;
    outResult->hasOverlay = 0;

    std::wstring wPath(filePath);
    auto analyzer = DocumentAnalyzerFactory::CreateAnalyzer(wPath);
    if (!analyzer) {
        strncpy_s(outResult->detectedFormat, sizeof(outResult->detectedFormat), "UNKNOWN", _TRUNCATE);
        strncpy_s(outResult->statusMessage, sizeof(outResult->statusMessage), "지원하지 않는 확장자", _TRUNCATE);
        return 1;
    }

    FILE* fp = nullptr;
    if (_wfopen_s(&fp, filePath, L"rb") != 0 || !fp) {
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

/* -------------------------------------------------------------------------
 * 4. Forensic Disk Image Inspection (.E01 / .DD / .RAW)
 * ------------------------------------------------------------------------- */
NATIVE_API int32_t __stdcall Engine_InspectForensicImage(const wchar_t* imagePath, ImageInspectionOutput* outResult) {
    char logBuf[512];
    sprintf_s(logBuf, "Engine_InspectForensicImage ENTRY: %ls", imagePath ? imagePath : L"(null)");
    EngineStatusManager::GetInstance().LogMessage(1, logBuf);

    if (!imagePath || !outResult) return -1;
    memset(outResult, 0, sizeof(ImageInspectionOutput));

    std::wstring wPath(imagePath);
    std::string lastErr;

    /* Create appropriate E01 or DD reader via ImageReaderFactory */
    auto reader = ImageReaderFactory::CreateAndOpen(wPath, &lastErr);
    if (!reader) {
        outResult->isValid = false;
        if (!lastErr.empty())
            strcpy_s(outResult->errorMessage, lastErr.c_str());
        else
            strcpy_s(outResult->errorMessage, "이미지 파일 오픈 실패");
        return -2;
    }

    /* Inspect SleuthKit FileSystem */
    TskFileSystemModule fsModule;
    if (!fsModule.InspectImage(reader.get(), outResult)) {
        outResult->isValid = false;
        return -3;
    }

    return 0;
}

/* -------------------------------------------------------------------------
 * 5. Extract Document Files (.hwp, .hwpx, .pdf) from Image
 * ------------------------------------------------------------------------- */
NATIVE_API int32_t __stdcall Engine_ExtractDocumentFilesFromImage(
    const wchar_t* imagePath,
    const wchar_t* tempExtractDir,
    int32_t includeDeleted,
    ImageScanProgressCallback callback,
    int32_t* outExtractedCount)
{
    if (!imagePath || !tempExtractDir) return -1;
    if (outExtractedCount) *outExtractedCount = 0;

    std::wstring wPath(imagePath);
    std::wstring wTempDir(tempExtractDir);
    std::string lastErr;

    auto reader = ImageReaderFactory::CreateAndOpen(wPath, &lastErr);
    if (!reader) return -2;

    TskFileSystemModule fsModule;
    bool ok = fsModule.ExtractDocuments(reader.get(), wTempDir, includeDeleted != 0, callback, outExtractedCount);

    return ok ? 0 : -3;
}

} /* extern "C" */
