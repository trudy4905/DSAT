#ifndef CHECK_HWPX_ZIP_OVERLAY_H
#define CHECK_HWPX_ZIP_OVERLAY_H

#include "../DocumentAnalyzerBase.h"
#include <cstdio>
#include <cstdint>

// HWPX (ZIP 구조) EOCD 탐색 기반 논리적 크기 및 오버레이 탐지
DetectionFinding CheckHwpxZipOverlay(FILE* fp, uint64_t fileSize, uint64_t& outLogicalSize);

#endif /* CHECK_HWPX_ZIP_OVERLAY_H */
