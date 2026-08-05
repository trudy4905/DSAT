#ifndef CHECK_HWP_OLE_OVERLAY_H
#define CHECK_HWP_OLE_OVERLAY_H

#include "../DocumentAnalyzerBase.h"
#include <cstdio>
#include <cstdint>

// HWP 파일의 OLE FAT/DIFAT 구조 분석을 통한 논리적 크기 및 오버레이 탐지
DetectionFinding CheckHwpOleOverlay(FILE* fp, uint64_t fileSize, uint64_t& outLogicalSize);

#endif /* CHECK_HWP_OLE_OVERLAY_H */
