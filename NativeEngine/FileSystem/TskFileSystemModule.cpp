#include "TskFileSystemModule.h"
#include "../Managers/EngineStatusManager.h"
#include <filesystem>
#include <fstream>
#include <algorithm>
#include <vector>
#include <stdio.h>
#include <string.h>
#include <windows.h>

namespace fs = std::filesystem;

// UTF-8 char* -> std::wstring (Windows API, 한글 등 멀티바이트 올바르게 처리)
static std::wstring Utf8ToWide(const std::string& utf8) {
    if (utf8.empty()) return L"";
    int len = MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, nullptr, 0);
    if (len <= 0) return L"";
    std::wstring result(len - 1, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, utf8.c_str(), -1, &result[0], len);
    return result;
}

// std::wstring -> UTF-8 std::string
static std::string WideToUtf8(const std::wstring& wide) {
    if (wide.empty()) return "";
    int len = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (len <= 0) return "";
    std::string result(len - 1, '\0');
    WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), -1, &result[0], len, nullptr, nullptr);
    return result;
}

struct WalkContext {
    TSK_FS_INFO*              fsInfo;
    const wchar_t*            extractDir;
    ImageScanProgressCallback callback;
    int*                      pCount;
    int                       count;
    bool                      includeDeleted;
};

static TSK_WALK_RET_ENUM MetaWalkCallback(TSK_FS_FILE* fsFile, void* ptr) {
    try {
        if (!fsFile || !fsFile->meta) return TSK_WALK_CONT;

        WalkContext* ctx = (WalkContext*)ptr;

        /* 1. Report live metadata scanning progress periodically (throttled every 10000 inodes) */
        if (fsFile->meta->type == TSK_FS_META_TYPE_DIR) {
            if (ctx && ctx->callback && (fsFile->meta->addr % 10000 == 0)) {
                std::wstring statusMsg = L"MFT 메타데이터 분석 중 (" + std::to_wstring(fsFile->meta->addr) + L")";
                ctx->callback(-1, statusMsg.c_str(), L"SCANNING_DIR");
            }
            return TSK_WALK_CONT;
        }

        /* 2. Only process regular files with valid metadata & non-zero size */
        if (fsFile->meta->type != TSK_FS_META_TYPE_REG || fsFile->meta->size <= 0) {
            return TSK_WALK_CONT;
        }

        // Skip files > 200MB to prevent memory exhaustion / corrupt metadata size hangs
        const TSK_OFF_T MAX_ALLOWED_FILE_SIZE = 200ULL * 1024ULL * 1024ULL;
        if (fsFile->meta->size > MAX_ALLOWED_FILE_SIZE) {
            return TSK_WALK_CONT;
        }

        // Extract filename from fsFile->name or fsFile->meta->name2
        const char* rawName = nullptr;
        if (fsFile->name && fsFile->name->name && fsFile->name->name[0] != '\0') {
            rawName = fsFile->name->name;
        } else if (fsFile->meta->name2 && fsFile->meta->name2->name && fsFile->meta->name2->name[0] != '\0') {
            rawName = fsFile->meta->name2->name;
        }

        if (!rawName) {
            return TSK_WALK_CONT;
        }

        /* Filter out directory references "." and ".." */
        if (rawName[0] == '.' && (rawName[1] == '\0' || (rawName[1] == '.' && rawName[2] == '\0'))) {
            return TSK_WALK_CONT;
        }

        bool isDeleted = false;
        if ((fsFile->name && (fsFile->name->flags & TSK_FS_NAME_FLAG_UNALLOC)) ||
            (fsFile->meta && (fsFile->meta->flags & TSK_FS_META_FLAG_UNALLOC))) {
            isDeleted = true;
        }

        std::string name(rawName);

        /* Extension matching */
        std::string ext;
        size_t dot = name.find_last_of('.');
        if (dot != std::string::npos) {
            ext = name.substr(dot);
            std::transform(ext.begin(), ext.end(), ext.begin(), [](unsigned char c){ return (char)::tolower(c); });
        }

        if (ext != ".hwp" && ext != ".hwpx" && ext != ".pdf") return TSK_WALK_CONT;

        /* Sanitize filename for Windows filesystem */
        std::string cleanName = name;
        for (char& c : cleanName) {
            if (c == '\\' || c == '/' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|') {
                c = '_';
            }
        }

        TSK_OFF_T fileSize = fsFile->meta->size;
        std::wstring wCleanName = Utf8ToWide(cleanName);

        // Logging
        {
            std::wstring wLogMsg = L"[TskMetaWalk] Found document ("
                                 + std::wstring(isDeleted ? L"DELETED" : L"ALLOCATED") + L"): " + wCleanName
                                 + L" (size " + std::to_wstring(fileSize) + L" bytes)";
            std::string utf8Log = WideToUtf8(wLogMsg);
            EngineStatusManager::GetInstance().LogMessage(1, utf8Log.c_str());
        }

        ctx->count++; // increment after successful file match

        std::wstring outFileName = std::to_wstring(ctx->count) + L"_" + wCleanName;
        fs::path outPath = fs::path(ctx->extractDir) / outFileName;

        std::ofstream ofs(outPath, std::ios::binary);
        if (!ofs.is_open()) return TSK_WALK_CONT;

        // Read in 64KB chunks to avoid large contiguous memory allocations
        const size_t CHUNK_SIZE = 64 * 1024;
        std::vector<char> chunkBuf(CHUNK_SIZE);
        TSK_OFF_T offset = 0;
        while (offset < fileSize) {
            size_t bytesToRead = (size_t)((std::min)((TSK_OFF_T)CHUNK_SIZE, fileSize - offset));
            ssize_t readBytes = tsk_fs_file_read(fsFile, offset, chunkBuf.data(), bytesToRead, TSK_FS_FILE_READ_FLAG_NONE);
            if (readBytes <= 0) break;
            ofs.write(chunkBuf.data(), readBytes);
            offset += readBytes;
        }
        ofs.close();

        if (offset == 0) {
            fs::remove(outPath);
            return TSK_WALK_CONT;
        }

        if (ctx->callback) {
            std::wstring fullPathW = outPath.wstring();
            std::wstring virtualPathW = L"\\" + wCleanName;

            if (fs::exists(fullPathW)) {
                std::wstring statusArg = L"FILE:" + std::wstring(isDeleted ? L"IS_DELETED:1:" : L"IS_DELETED:0:") + virtualPathW;
                ctx->callback(ctx->count, fullPathW.c_str(), statusArg.c_str());
            }
        }
        if (ctx->pCount) *ctx->pCount = ctx->count;

    } catch (...) {
        // Prevent any C++ exception from escaping into C SleuthKit callback
    }
    return TSK_WALK_CONT;
}

static void ScanFilesystem(TSK_FS_INFO* fsInfo, WalkContext* ctx) {
    if (!fsInfo) return;
    int flags = (int)TSK_FS_META_FLAG_ALLOC;
    if (ctx && ctx->includeDeleted) {
        flags |= (int)TSK_FS_META_FLAG_UNALLOC;
    }
    ctx->fsInfo = fsInfo;
    tsk_fs_meta_walk(fsInfo, fsInfo->first_inum, fsInfo->last_inum,
                     (TSK_FS_META_FLAG_ENUM)flags,
                     MetaWalkCallback, ctx);
}

static void ScanAllPartitions(TSK_IMG_INFO* imgInfo, WalkContext* ctx) {
    if (!imgInfo || imgInfo->size < 512) return;

    /* Try MBR/GPT volume system first */
    TSK_VS_INFO* vsInfo = tsk_vs_open(imgInfo, 0, TSK_VS_TYPE_DETECT);
    if (vsInfo) {
        for (TSK_PNUM_T i = 0; i < vsInfo->part_count; i++) {
            const TSK_VS_PART_INFO* part = tsk_vs_part_get(vsInfo, i);
            if (!part) continue;

            if (part->flags & TSK_VS_PART_FLAG_ALLOC) {
                TSK_FS_INFO* fsInfo = tsk_fs_open_vol(part, TSK_FS_TYPE_DETECT);
                if (fsInfo) {
                    ScanFilesystem(fsInfo, ctx);
                    tsk_fs_close(fsInfo);
                } else {
                    tsk_error_reset();
                }
            }
        }
        tsk_vs_close(vsInfo);
        return;
    }

    tsk_error_reset();

    /* Fallback: open raw image filesystem directly */
    TSK_FS_INFO* fsInfo = tsk_fs_open_img(imgInfo, 0, TSK_FS_TYPE_DETECT);
    if (fsInfo) {
        ScanFilesystem(fsInfo, ctx);
        tsk_fs_close(fsInfo);
    } else {
        tsk_error_reset();
    }
}

TskFileSystemModule::TskFileSystemModule() {}
TskFileSystemModule::~TskFileSystemModule() {}

bool TskFileSystemModule::InspectImage(
    IImageReader* reader,
    ImageInspectionOutput* outResult)
{
    if (!reader || !outResult) return false;
    memset(outResult, 0, sizeof(ImageInspectionOutput));

    TSK_IMG_INFO* img = reader->CreateTskAdapter();
    if (!img) {
        outResult->isValid = false;
        strcpy_s(outResult->errorMessage, "TSK 이미지 어댑터 생성 실패");
        return false;
    }

    outResult->isValid = true;
    std::string tag = reader->GetTypeTag();
    strcpy_s(outResult->imageTypeTag, tag.c_str());
    outResult->totalImageSize = (uint64_t)reader->GetSize();
    outResult->totalPartitionSize = 0;
    outResult->partitionCount = 0;

    char logMsg[256];
    sprintf_s(logMsg, "[TskFileSystemModule] InspectImage size: %lld bytes", img->size);
    EngineStatusManager::GetInstance().LogMessage(1, logMsg);

    /* 1. Try Volume System (MBR / GPT) if image size >= 1 sector (512 bytes) */
    TSK_VS_INFO* vsInfo = NULL;
    if (img->size >= 512) {
        EngineStatusManager::GetInstance().LogMessage(1, "[TskFileSystemModule] Calling tsk_vs_open...");
        vsInfo = tsk_vs_open(img, 0, TSK_VS_TYPE_DETECT);
        sprintf_s(logMsg, "[TskFileSystemModule] tsk_vs_open result: %s", vsInfo ? "SUCCESS" : "NULL (No VS)");
        EngineStatusManager::GetInstance().LogMessage(1, logMsg);
    }
    if (vsInfo) {
        int pIndex = 0;
        for (TSK_PNUM_T i = 0; i < vsInfo->part_count && pIndex < 16; i++) {
            const TSK_VS_PART_INFO* part = tsk_vs_part_get(vsInfo, i);
            if (!part) continue;

            PartitionItemInfo& item = outResult->partitions[pIndex];
            item.partitionIndex = pIndex + 1;
            item.sectorSize = vsInfo->block_size > 0 ? vsInfo->block_size : 512;
            item.startSector = part->start;
            item.sectorCount = part->len;
            item.isSupported = false;

            outResult->totalPartitionSize += (part->len * (uint64_t)item.sectorSize);

            if (part->desc && part->desc[0] != '\0') {
                strncpy_s(item.filesystem, part->desc, sizeof(item.filesystem) - 1);
            } else {
                strcpy_s(item.filesystem, "Unallocated/Raw");
            }

            /* Only attempt filesystem detection on allocated partitions */
            if (part->flags & TSK_VS_PART_FLAG_ALLOC) {
                TSK_FS_INFO* fsInfo = tsk_fs_open_vol(part, TSK_FS_TYPE_DETECT);
                if (fsInfo) {
                    const char* fsName = tsk_fs_type_toname(fsInfo->ftype);
                    if (fsName && fsName[0] != '\0') {
                        strncpy_s(item.filesystem, fsName, sizeof(item.filesystem) - 1);
                    }
                    item.isSupported = true;
                    tsk_fs_close(fsInfo);
                } else {
                    tsk_error_reset();
                }
            }
            pIndex++;
        }
        outResult->partitionCount = pIndex;
        tsk_vs_close(vsInfo);
    } else {
        tsk_error_reset();

        /* 2. Fallback: Raw image filesystem without volume system */
        outResult->partitionCount = 1;
        outResult->totalPartitionSize = (uint64_t)img->size;
        PartitionItemInfo& item = outResult->partitions[0];
        item.partitionIndex = 1;
        item.sectorSize = 512;
        item.startSector = 0;
        item.sectorCount = (uint64_t)(img->size / 512);
        item.isSupported = false;

        if (img->size >= 512) {
            TSK_FS_INFO* fsInfo = tsk_fs_open_img(img, 0, TSK_FS_TYPE_DETECT);
            if (fsInfo) {
                const char* fsName = tsk_fs_type_toname(fsInfo->ftype);
                if (fsName && fsName[0] != '\0') {
                    strncpy_s(item.filesystem, fsName, sizeof(item.filesystem) - 1);
                } else {
                    strcpy_s(item.filesystem, "Raw FS");
                }
                item.isSupported = true;
                tsk_fs_close(fsInfo);
            } else {
                tsk_error_reset();
                strcpy_s(item.filesystem, "Raw/Unrecognized FS");
            }
        } else {
            strcpy_s(item.filesystem, "Non-disk File (Small)");
        }
    }

    img->close(img);
    return true;
}

bool TskFileSystemModule::ExtractDocuments(
    IImageReader* reader,
    const std::wstring& tempExtractDir,
    bool includeDeleted,
    ImageScanProgressCallback callback,
    int* outExtractedCount)
{
    if (!reader) return false;
    if (outExtractedCount) *outExtractedCount = 0;

    TSK_IMG_INFO* imgInfo = reader->CreateTskAdapter();
    if (!imgInfo) return false;

    fs::create_directories(fs::path(tempExtractDir));

    WalkContext ctx;
    ctx.fsInfo         = NULL;
    ctx.extractDir     = tempExtractDir.c_str();
    ctx.callback       = callback;
    ctx.pCount         = outExtractedCount;
    ctx.count          = 0;
    ctx.includeDeleted = includeDeleted;

    ScanAllPartitions(imgInfo, &ctx);

    imgInfo->close(imgInfo);
    if (outExtractedCount) *outExtractedCount = ctx.count;
    return true;
}
