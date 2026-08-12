#pragma once

#include "IImageReader.h"
#include <windows.h>
#include <vector>
#include <string>

struct DdSegment {
    std::wstring filePath;
    HANDLE       hFile;
    int64_t      startOffset;
    int64_t      fileSize;
};

class DdImageReader : public IImageReader {
public:
    DdImageReader();
    virtual ~DdImageReader();

    bool Open(const std::wstring& imagePath) override;
    ssize_t Read(int64_t offset, char* buffer, size_t length) override;
    int64_t GetSize() const override;
    void Close() override;
    std::string GetTypeTag() const override;
    std::string GetLastErrorMessage() const override;
    TSK_IMG_INFO* CreateTskAdapter() override;

private:
    std::vector<DdSegment> CollectDdSegments(const std::wstring& mainPath);

    std::vector<DdSegment> m_segments;
    int64_t                m_totalSize;
    std::string            m_lastError;
};
