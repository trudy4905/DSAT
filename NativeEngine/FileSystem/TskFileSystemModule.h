#pragma once

#include "../NativeEngineApi.h"
#include "../ImageReaders/IImageReader.h"
#include <tsk/libtsk.h>
#include <string>

class TskFileSystemModule {
public:
    TskFileSystemModule();
    ~TskFileSystemModule();

    /* Inspects partitions & filesystem types from the image reader */
    bool InspectImage(
        IImageReader* reader,
        ImageInspectionOutput* outResult);

    /* Extracts targeted document files (.hwp, .hwpx, .pdf) from the image */
    bool ExtractDocuments(
        IImageReader* reader,
        const std::wstring& tempExtractDir,
        bool includeDeleted,
        ImageScanProgressCallback callback,
        int* outExtractedCount);
};
