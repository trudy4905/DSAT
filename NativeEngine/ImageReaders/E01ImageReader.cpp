#include "E01ImageReader.h"
#include "../Managers/EngineStatusManager.h"
#include <tsk/libtsk.h>
#include <tsk/base/tsk_base_i.h>
#include <filesystem>
#include <algorithm>
#include <windows.h>
#include <stdio.h>

namespace fs = std::filesystem;

struct E01TskAdapter {
    TSK_IMG_INFO    base;
    E01ImageReader* reader;
};

static void E01TskAdapterClose(TSK_IMG_INFO* img) {
    if (!img) return;
    E01TskAdapter* a = (E01TskAdapter*)img;
    tsk_deinit_lock(&(a->base.cache_lock));
    free(a);
}

static ssize_t E01TskAdapterRead(TSK_IMG_INFO* img, TSK_OFF_T offset, char* buf, size_t len) {
    if (!img || !buf) return -1;
    E01TskAdapter* a = (E01TskAdapter*)img;
    if (a->reader) {
        return a->reader->Read((int64_t)offset, buf, len);
    }
    return -1;
}

static void E01TskAdapterStat(TSK_IMG_INFO*, FILE*) {}

E01ImageReader::E01ImageReader()
    : m_ewfHandle(nullptr)
    , m_mediaSize(0)
    , m_segmentCount(1)
{
}

E01ImageReader::~E01ImageReader() {
    Close();
}

std::vector<std::wstring> E01ImageReader::CollectE01Segments(const std::wstring& mainPath) {
    std::vector<std::wstring> segments;
    try {
        fs::path p = fs::absolute(fs::path(mainPath));
        fs::path parentDir = p.parent_path();
        std::wstring targetStem = p.stem().wstring();
        std::transform(targetStem.begin(), targetStem.end(), targetStem.begin(), [](wchar_t c) -> wchar_t {
            return (c >= L'A' && c <= L'Z') ? static_cast<wchar_t>(c + (L'a' - L'A')) : c;
        });

        if (fs::exists(parentDir) && fs::is_directory(parentDir)) {
            for (const auto& entry : fs::directory_iterator(parentDir)) {
                if (!entry.is_regular_file()) continue;

                fs::path entryPath = entry.path();
                std::wstring stem = entryPath.stem().wstring();
                std::transform(stem.begin(), stem.end(), stem.begin(), [](wchar_t c) -> wchar_t {
                    return (c >= L'A' && c <= L'Z') ? static_cast<wchar_t>(c + (L'a' - L'A')) : c;
                });

                if (stem == targetStem) {
                    std::wstring ext = entryPath.extension().wstring();
                    std::transform(ext.begin(), ext.end(), ext.begin(), [](wchar_t c) -> wchar_t {
                        return (c >= L'A' && c <= L'Z') ? static_cast<wchar_t>(c + (L'a' - L'A')) : c;
                    });

                    /* Match EWF segment extensions (.e01-.e99, .eaa-.ezz, .ex01, .l01, .s01) */
                    if (ext.length() >= 4 && (ext[1] == L'e' || ext[1] == L'l' || ext[1] == L's')) {
                        segments.push_back(entryPath.wstring());
                    }
                }
            }
        }
    } catch (...) {}

    std::sort(segments.begin(), segments.end(), [](const std::wstring& a, const std::wstring& b) {
        std::wstring sa = a, sb = b;
        std::transform(sa.begin(), sa.end(), sa.begin(), [](wchar_t c) -> wchar_t {
            return (c >= L'A' && c <= L'Z') ? static_cast<wchar_t>(c + (L'a' - L'A')) : c;
        });
        std::transform(sb.begin(), sb.end(), sb.begin(), [](wchar_t c) -> wchar_t {
            return (c >= L'A' && c <= L'Z') ? static_cast<wchar_t>(c + (L'a' - L'A')) : c;
        });
        return sa < sb;
    });

    if (segments.empty()) {
        segments.push_back(mainPath);
    }
    return segments;
}

bool E01ImageReader::Open(const std::wstring& imagePath) {
    Close();
    m_lastError.clear();

    char dbgBuf[512];
    sprintf_s(dbgBuf, "E01ImageReader::Open entering for path: %ls", imagePath.c_str());
    EngineStatusManager::GetInstance().LogMessage(1, dbgBuf);

    /* 0. Pre-check file sharing and access */
    HANDLE hTest = CreateFileW(imagePath.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                               NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hTest == INVALID_HANDLE_VALUE) {
        DWORD dwErr = GetLastError();
        char errBuf[256];
        sprintf_s(errBuf, "E01 이미지 파일 접근 실패 (Win32 Error: %lu - %s)",
                  dwErr, (dwErr == 32) ? "다른 프로세스에서 파일 사용 중 (Sharing Violation)" :
                         (dwErr == 2)  ? "파일을 찾을 수 없음 (File Not Found)" : "접근 거부/시스템 오류");
        m_lastError = errBuf;
        EngineStatusManager::GetInstance().LogMessage(3, errBuf);
        return false;
    }
    CloseHandle(hTest);

    libewf_error_t* err = NULL;

    /* 1. E01 Header Signature Check */
    int sigCheck = libewf_check_file_signature_wide(imagePath.c_str(), &err);
    if (err) { libewf_error_free(&err); err = NULL; }
    if (sigCheck == 0) {
        m_lastError = "E01 헤더 시그니처(EVF)를 찾을 수 없습니다. 올바른 E01 이미지 파일이 아닙니다.";
        EngineStatusManager::GetInstance().LogMessage(2, m_lastError.c_str());
        return false;
    }

    /* 2. Initialize libewf handle */
    if (libewf_handle_initialize(&m_ewfHandle, &err) != 1) {
        m_lastError = "libewf 핸들 초기화 실패";
        EngineStatusManager::GetInstance().LogMessage(3, m_lastError.c_str());
        return false;
    }

    int openRes = -1;

    /* 3-A. Primary: Native libewf segment globber */
    wchar_t** globbedFiles = NULL;
    int numGlobbed = 0;
    if (libewf_glob_wide(imagePath.c_str(), imagePath.length(), LIBEWF_FORMAT_UNKNOWN, &globbedFiles, &numGlobbed, &err) == 1 && numGlobbed > 0) {
        char dbgGlob[512];
        sprintf_s(dbgGlob, "libewf_glob_wide discovered %d segment files.", numGlobbed);
        EngineStatusManager::GetInstance().LogMessage(1, dbgGlob);

        openRes = libewf_handle_open_wide(m_ewfHandle, globbedFiles, numGlobbed, LIBEWF_OPEN_READ, &err);
        if (openRes == 1) m_segmentCount = (size_t)numGlobbed;
        libewf_glob_wide_free(globbedFiles, numGlobbed, NULL);
    }

    /* 3-B. Fallback: Custom directory segment discovery */
    if (openRes != 1) {
        if (err) { libewf_error_free(&err); err = NULL; }
        if (m_ewfHandle) {
            libewf_handle_close(m_ewfHandle, NULL);
            libewf_handle_free(&m_ewfHandle, NULL);
            m_ewfHandle = nullptr;
        }
        if (libewf_handle_initialize(&m_ewfHandle, &err) == 1) {
            std::vector<std::wstring> segPaths = CollectE01Segments(imagePath);
            std::vector<wchar_t*> segPtrs;
            for (auto& s : segPaths) segPtrs.push_back(const_cast<wchar_t*>(s.c_str()));

            if (!segPtrs.empty()) {
                char dbgCustom[512];
                sprintf_s(dbgCustom, "CollectE01Segments discovered %d segment files.", (int)segPtrs.size());
                EngineStatusManager::GetInstance().LogMessage(1, dbgCustom);

                openRes = libewf_handle_open_wide(m_ewfHandle, segPtrs.data(), (int)segPtrs.size(), LIBEWF_OPEN_READ, &err);
                if (openRes == 1) m_segmentCount = segPtrs.size();
            }
        }
    }

    /* 3-C. Fallback: Direct single file open */
    if (openRes != 1) {
        if (err) { libewf_error_free(&err); err = NULL; }
        if (m_ewfHandle) {
            libewf_handle_close(m_ewfHandle, NULL);
            libewf_handle_free(&m_ewfHandle, NULL);
            m_ewfHandle = nullptr;
        }
        if (libewf_handle_initialize(&m_ewfHandle, &err) == 1) {
            wchar_t* filenamesW[1] = { const_cast<wchar_t*>(imagePath.c_str()) };
            openRes = libewf_handle_open_wide(m_ewfHandle, filenamesW, 1, LIBEWF_OPEN_READ, &err);
            if (openRes == 1) m_segmentCount = 1;
        }
    }

    if (openRes == 1) {
        size64_t mediaSize = 0;
        libewf_handle_get_media_size(m_ewfHandle, &mediaSize, &err);
        m_mediaSize = (int64_t)mediaSize;

        char dbgOk[512];
        sprintf_s(dbgOk, "E01ImageReader successfully opened. Total media size: %lld bytes (%.2f GB)",
                  m_mediaSize, (double)m_mediaSize / (1024.0 * 1024.0 * 1024.0));
        EngineStatusManager::GetInstance().LogMessage(1, dbgOk);
        return true;
    }

    /* Open failed – capture error string */
    char errBuf[256] = {0};
    if (err) {
        libewf_error_sprint(err, errBuf, sizeof(errBuf));
        libewf_error_free(&err);
    }
    m_lastError = (errBuf[0] != '\0') ? std::string("libewf E01 파싱 실패: ") + errBuf
                                      : "libewf E01 이미지 파싱 오류";

    libewf_handle_free(&m_ewfHandle, NULL);
    m_ewfHandle = nullptr;
    return false;
}

ssize_t E01ImageReader::Read(int64_t offset, char* buffer, size_t length) {
    if (!m_ewfHandle || !buffer || offset < 0 || offset >= m_mediaSize) return 0;
    int64_t bytesLeft = m_mediaSize - offset;
    size_t toRead = (size_t)((bytesLeft < (int64_t)length) ? bytesLeft : (int64_t)length);
    if (toRead == 0) return 0;

    libewf_error_t* err = NULL;
    return (ssize_t)libewf_handle_read_random(m_ewfHandle, buffer, toRead, (off64_t)offset, &err);
}

int64_t E01ImageReader::GetSize() const {
    return m_mediaSize;
}

void E01ImageReader::Close() {
    if (m_ewfHandle) {
        libewf_error_t* err = NULL;
        libewf_handle_close(m_ewfHandle, &err);
        libewf_handle_free(&m_ewfHandle, &err);
        m_ewfHandle = nullptr;
    }
    m_mediaSize = 0;
}

std::string E01ImageReader::GetTypeTag() const {
    if (m_segmentCount > 1) {
        return "E01 (" + std::to_string(m_segmentCount) + " Segments)";
    }
    return "E01";
}

std::string E01ImageReader::GetLastErrorMessage() const {
    return m_lastError;
}

TSK_IMG_INFO* E01ImageReader::CreateTskAdapter() {
    E01TskAdapter* a = (E01TskAdapter*)calloc(1, sizeof(E01TskAdapter));
    if (!a) return NULL;

    uint32_t bytesPerSector = 512;
    if (m_ewfHandle) {
        libewf_error_t* err = NULL;
        uint32_t bps = 0;
        if (libewf_handle_get_bytes_per_sector(m_ewfHandle, &bps, &err) == 1 && bps > 0) {
            bytesPerSector = bps;
        }
        if (err) { libewf_error_free(&err); err = NULL; }
    }

    a->reader          = this;
    a->base.tag        = TSK_IMG_INFO_TAG;
    a->base.itype      = TSK_IMG_TYPE_EXTERNAL;
    a->base.size       = (TSK_OFF_T)m_mediaSize;
    a->base.sector_size = bytesPerSector;
    a->base.close      = E01TskAdapterClose;
    a->base.read       = E01TskAdapterRead;
    a->base.imgstat    = E01TskAdapterStat;

    tsk_init_lock(&(a->base.cache_lock));

    return (TSK_IMG_INFO*)a;
}
