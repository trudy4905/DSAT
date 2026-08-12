#include "DdImageReader.h"
#include "../Managers/EngineStatusManager.h"
#include <tsk/libtsk.h>
#include <tsk/base/tsk_base_i.h>
#include <filesystem>
#include <algorithm>
#include <regex>
#include <stdio.h>
#include <windows.h>
#include <winioctl.h>

namespace fs = std::filesystem;

struct DdTskAdapter {
    TSK_IMG_INFO   base;
    DdImageReader* reader;
};

static void DdTskAdapterClose(TSK_IMG_INFO* img) {
    if (!img) return;
    DdTskAdapter* a = (DdTskAdapter*)img;
    tsk_deinit_lock(&(a->base.cache_lock));
    free(a);
}

static ssize_t DdTskAdapterRead(TSK_IMG_INFO* img, TSK_OFF_T offset, char* buf, size_t len) {
    if (!img || !buf) return -1;
    DdTskAdapter* a = (DdTskAdapter*)img;
    if (a->reader) {
        return a->reader->Read((int64_t)offset, buf, len);
    }
    return -1;
}

static void DdTskAdapterStat(TSK_IMG_INFO*, FILE*) {}

DdImageReader::DdImageReader()
    : m_totalSize(0)
{
}

DdImageReader::~DdImageReader() {
    Close();
}

std::vector<DdSegment> DdImageReader::CollectDdSegments(const std::wstring& mainPath) {
    std::vector<DdSegment> segments;

    /* Check if mainPath is a Win32 Device Path (e.g. \\.\PhysicalDrive0 or \\.\C:) */
    if (mainPath.rfind(L"\\\\.\\", 0) == 0 || mainPath.rfind(L"\\\\?\\", 0) == 0) {
        HANDLE hDevice = CreateFileW(mainPath.c_str(), GENERIC_READ,
                                     FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                     NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
        if (hDevice != INVALID_HANDLE_VALUE) {
            LARGE_INTEGER fsz;
            fsz.QuadPart = 0;
            GetFileSizeEx(hDevice, &fsz);

            if (fsz.QuadPart == 0) {
                GET_LENGTH_INFORMATION lenInfo = { 0 };
                DWORD bytesReturned = 0;
                if (DeviceIoControl(hDevice, IOCTL_DISK_GET_LENGTH_INFO, NULL, 0, &lenInfo, sizeof(lenInfo), &bytesReturned, NULL)) {
                    fsz.QuadPart = lenInfo.Length.QuadPart;
                }
            }

            DdSegment seg;
            seg.filePath    = mainPath;
            seg.hFile       = hDevice;
            seg.startOffset = 0;
            seg.fileSize    = fsz.QuadPart;

            segments.push_back(seg);
            return segments;
        }
    }

    try {
        fs::path p = fs::absolute(fs::path(mainPath));
        fs::path parentDir = p.parent_path();
        std::wstring fileName = p.filename().wstring();

        /* Determine base prefix and pattern matching for split DD files (.001, .002... or .dd.001, .dd.002...) */
        std::wregex segRegex(L"^(.*?)(?:\\.(?:dd|raw|img|001|\\d{3,4}))?$", std::regex_constants::icase);
        std::wsmatch match;

        std::wstring stemPrefix = p.stem().wstring();
        if (std::regex_match(fileName, match, segRegex) && match.size() > 1) {
            std::wstring group1 = match[1].str();
            if (!group1.empty()) stemPrefix = group1;
        }

        std::transform(stemPrefix.begin(), stemPrefix.end(), stemPrefix.begin(), [](wchar_t c) -> wchar_t {
            return (c >= L'A' && c <= L'Z') ? static_cast<wchar_t>(c + (L'a' - L'A')) : c;
        });

        std::vector<fs::path> candidatePaths;
        if (fs::exists(parentDir) && fs::is_directory(parentDir)) {
            for (const auto& entry : fs::directory_iterator(parentDir)) {
                if (!entry.is_regular_file()) continue;

                fs::path entryPath = entry.path();
                std::wstring fName = entryPath.filename().wstring();
                std::transform(fName.begin(), fName.end(), fName.begin(), [](wchar_t c) -> wchar_t {
                    return (c >= L'A' && c <= L'Z') ? static_cast<wchar_t>(c + (L'a' - L'A')) : c;
                });

                if (fName.rfind(stemPrefix, 0) == 0) {
                    std::wstring ext = entryPath.extension().wstring();
                    std::transform(ext.begin(), ext.end(), ext.begin(), [](wchar_t c) -> wchar_t {
                        return (c >= L'A' && c <= L'Z') ? static_cast<wchar_t>(c + (L'a' - L'A')) : c;
                    });

                    /* Match numeric extensions (.001-.999, .0001-.9999) or raw/dd extensions */
                    if (std::regex_match(ext, std::wregex(L"^\\.(?:raw|dd|img|\\d{3,4})$", std::regex_constants::icase))) {
                        candidatePaths.push_back(entryPath);
                    }
                }
            }
        }

        /* Sort candidate segment paths numerically/alphabetically */
        std::sort(candidatePaths.begin(), candidatePaths.end(), [](const fs::path& a, const fs::path& b) {
            std::wstring sa = a.wstring(), sb = b.wstring();
            std::transform(sa.begin(), sa.end(), sa.begin(), [](wchar_t c) -> wchar_t {
                return (c >= L'A' && c <= L'Z') ? static_cast<wchar_t>(c + (L'a' - L'A')) : c;
            });
            std::transform(sb.begin(), sb.end(), sb.begin(), [](wchar_t c) -> wchar_t {
                return (c >= L'A' && c <= L'Z') ? static_cast<wchar_t>(c + (L'a' - L'A')) : c;
            });
            return sa < sb;
        });

        int64_t currentOffset = 0;
        for (const auto& path : candidatePaths) {
            HANDLE hFile = CreateFileW(path.wstring().c_str(), GENERIC_READ,
                                       FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                       NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
            if (hFile != INVALID_HANDLE_VALUE) {
                LARGE_INTEGER fsz;
                GetFileSizeEx(hFile, &fsz);

                DdSegment seg;
                seg.filePath    = path.wstring();
                seg.hFile       = hFile;
                seg.startOffset = currentOffset;
                seg.fileSize    = fsz.QuadPart;

                currentOffset += fsz.QuadPart;
                segments.push_back(seg);
            }
        }
    } catch (...) {}

    /* Fallback to opening single mainPath file if no segments were successfully opened */
    if (segments.empty()) {
        HANDLE hFile = CreateFileW(mainPath.c_str(), GENERIC_READ,
                                   FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                   NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
        if (hFile != INVALID_HANDLE_VALUE) {
            LARGE_INTEGER fsz;
            GetFileSizeEx(hFile, &fsz);

            DdSegment seg;
            seg.filePath    = mainPath;
            seg.hFile       = hFile;
            seg.startOffset = 0;
            seg.fileSize    = fsz.QuadPart;

            segments.push_back(seg);
        }
    }

    return segments;
}

bool DdImageReader::Open(const std::wstring& imagePath) {
    Close();
    m_lastError.clear();

    m_segments = CollectDdSegments(imagePath);
    if (m_segments.empty()) {
        DWORD dwErr = GetLastError();
        char errBuf[256];
        sprintf_s(errBuf, "DD/RAW 이미지 접근 실패 (Win32 Error: %lu - %s)",
                  dwErr, (dwErr == 32) ? "다른 프로세스에서 파일 사용 중 (Sharing Violation)" :
                         (dwErr == 2)  ? "파일을 찾을 수 없음 (File Not Found)" : "접근 거부/시스템 오류");
        m_lastError = errBuf;
        EngineStatusManager::GetInstance().LogMessage(3, errBuf);
        return false;
    }

    m_totalSize = 0;
    for (const auto& seg : m_segments) {
        m_totalSize += seg.fileSize;
    }

    char logBuf[512];
    sprintf_s(logBuf, "[DdImageReader] Successfully opened %zu DD segment(s). Total Size: %lld bytes",
              m_segments.size(), m_totalSize);
    EngineStatusManager::GetInstance().LogMessage(1, logBuf);

    return true;
}

ssize_t DdImageReader::Read(int64_t offset, char* buffer, size_t length) {
    if (m_segments.empty() || !buffer || offset < 0 || offset >= m_totalSize) return 0;

    int64_t bytesToRead = (int64_t)length;
    if (offset + bytesToRead > m_totalSize) {
        bytesToRead = m_totalSize - offset;
    }
    if (bytesToRead <= 0) return 0;

    int64_t totalRead = 0;
    int64_t currentOffset = offset;

    for (const auto& seg : m_segments) {
        if (totalRead >= bytesToRead) break;

        int64_t segEnd = seg.startOffset + seg.fileSize;
        if (currentOffset >= seg.startOffset && currentOffset < segEnd) {
            int64_t offsetInSeg = currentOffset - seg.startOffset;
            int64_t availableInSeg = seg.fileSize - offsetInSeg;
            int64_t chunkToRead = (bytesToRead - totalRead < availableInSeg) ? (bytesToRead - totalRead) : availableInSeg;

            LARGE_INTEGER li;
            li.QuadPart = offsetInSeg;
            SetFilePointerEx(seg.hFile, li, NULL, FILE_BEGIN);

            DWORD readBytes = 0;
            if (ReadFile(seg.hFile, buffer + totalRead, (DWORD)chunkToRead, &readBytes, NULL)) {
                if (readBytes == 0) break;
                totalRead += readBytes;
                currentOffset += readBytes;
            } else {
                break;
            }
        }
    }

    return (ssize_t)totalRead;
}

int64_t DdImageReader::GetSize() const {
    return m_totalSize;
}

void DdImageReader::Close() {
    for (auto& seg : m_segments) {
        if (seg.hFile != INVALID_HANDLE_VALUE) {
            CloseHandle(seg.hFile);
            seg.hFile = INVALID_HANDLE_VALUE;
        }
    }
    m_segments.clear();
    m_totalSize = 0;
}

std::string DdImageReader::GetTypeTag() const {
    if (m_segments.size() > 1) {
        return "DD/RAW (" + std::to_string(m_segments.size()) + " Segments)";
    }
    return "DD/RAW";
}

std::string DdImageReader::GetLastErrorMessage() const {
    return m_lastError;
}

TSK_IMG_INFO* DdImageReader::CreateTskAdapter() {
    DdTskAdapter* a = (DdTskAdapter*)calloc(1, sizeof(DdTskAdapter));
    if (!a) return NULL;

    a->reader          = this;
    a->base.tag        = TSK_IMG_INFO_TAG;
    a->base.itype      = TSK_IMG_TYPE_EXTERNAL;
    a->base.size       = (TSK_OFF_T)m_totalSize;
    a->base.sector_size = 512;
    a->base.close      = DdTskAdapterClose;
    a->base.read       = DdTskAdapterRead;
    a->base.imgstat    = DdTskAdapterStat;

    tsk_init_lock(&(a->base.cache_lock));

    return (TSK_IMG_INFO*)a;
}
