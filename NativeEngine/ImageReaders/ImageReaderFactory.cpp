#define LIBEWF_HAVE_WIDE_CHARACTER_TYPE
#include <libewf.h>
#include "ImageReaderFactory.h"
#include "E01ImageReader.h"
#include "DdImageReader.h"
#include <filesystem>
#include <algorithm>
#include <windows.h>
#include <stdio.h>

namespace fs = std::filesystem;

std::unique_ptr<IImageReader> ImageReaderFactory::CreateAndOpen(
    const std::wstring& imagePath,
    std::string* outLastError)
{
    if (outLastError) outLastError->clear();

    char dbgBuf[512];
    sprintf_s(dbgBuf, "[NativeEngine] ImageReaderFactory::CreateAndOpen called for: %ls\n", imagePath.c_str());
    OutputDebugStringA(dbgBuf);
    printf("%s", dbgBuf); fflush(stdout);

    fs::path p(imagePath);
    std::wstring ext = p.extension().wstring();
    std::transform(ext.begin(), ext.end(), ext.begin(), ::towlower);

    /* 1. Check E01 header signature via libewf */
    libewf_error_t* err = NULL;
    int sigCheck = libewf_check_file_signature_wide(imagePath.c_str(), &err);
    if (err) { libewf_error_free(&err); err = NULL; }

    /* 2. Check EWF extension pattern (.e01-.e99, .eaa-.ezz, .ex01, .l01, .s01) */
    bool extIsEwf = (ext.length() >= 4 && (ext[1] == L'e' || ext[1] == L'l' || ext[1] == L's'));

    bool isE01 = (sigCheck == 1) || extIsEwf;

    std::unique_ptr<IImageReader> reader;
    if (isE01) {
        OutputDebugStringA("[NativeEngine] Factory selected E01ImageReader\n");
        printf("[NativeEngine] Factory selected E01ImageReader\n"); fflush(stdout);
        reader = std::make_unique<E01ImageReader>();
    } else {
        OutputDebugStringA("[NativeEngine] Factory selected DdImageReader\n");
        printf("[NativeEngine] Factory selected DdImageReader\n"); fflush(stdout);
        reader = std::make_unique<DdImageReader>();
    }

    if (reader->Open(imagePath)) {
        return reader;
    }

    if (outLastError) {
        *outLastError = reader->GetLastErrorMessage();
    }
    return nullptr;
}
