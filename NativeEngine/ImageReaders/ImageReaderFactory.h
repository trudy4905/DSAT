#pragma once

#include "IImageReader.h"
#include <memory>
#include <string>

class ImageReaderFactory {
public:
    /* Creates and opens an appropriate IImageReader based on file extension (.e01, .dd, .raw, etc.).
       Returns nullptr on failure; check outLastError if provided. */
    static std::unique_ptr<IImageReader> CreateAndOpen(
        const std::wstring& imagePath,
        std::string* outLastError = nullptr);
};
