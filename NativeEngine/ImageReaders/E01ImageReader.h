#pragma once

#include "IImageReader.h"
#define LIBEWF_HAVE_WIDE_CHARACTER_TYPE
#include <libewf.h>

class E01ImageReader : public IImageReader {
public:
    E01ImageReader();
    virtual ~E01ImageReader();

    bool Open(const std::wstring& imagePath) override;
    ssize_t Read(int64_t offset, char* buffer, size_t length) override;
    int64_t GetSize() const override;
    void Close() override;
    std::string GetTypeTag() const override;
    std::string GetLastErrorMessage() const override;
    TSK_IMG_INFO* CreateTskAdapter() override;

private:
    libewf_handle_t* m_ewfHandle;
    int64_t          m_mediaSize;
    size_t           m_segmentCount;
    std::string      m_lastError;

    static std::vector<std::wstring> CollectE01Segments(const std::wstring& mainPath);
};
