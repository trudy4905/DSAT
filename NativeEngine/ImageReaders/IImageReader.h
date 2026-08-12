#pragma once

#include <string>
#include <vector>
#include <stdint.h>

#if defined(_MSC_VER) && !defined(ssize_t)
typedef intptr_t ssize_t;
#endif

/* Forward declaration of TSK_IMG_INFO struct from Sleuth Kit */
struct TSK_IMG_INFO;

class IImageReader {
public:
    virtual ~IImageReader() = default;

    /* Opens the image file(s) */
    virtual bool Open(const std::wstring& imagePath) = 0;

    /* Reads data at specified offset into buffer */
    virtual ssize_t Read(int64_t offset, char* buffer, size_t length) = 0;

    /* Returns total size of image in bytes */
    virtual int64_t GetSize() const = 0;

    /* Closes the image file(s) */
    virtual void Close() = 0;

    /* Returns description or type tag (e.g. "E01 (libewf)" or "DD/RAW") */
    virtual std::string GetTypeTag() const = 0;

    /* Returns last error message string */
    virtual std::string GetLastErrorMessage() const = 0;

    /* Creates a TSK_IMG_INFO adapter wrapping this reader for SleuthKit */
    virtual TSK_IMG_INFO* CreateTskAdapter() = 0;
};
