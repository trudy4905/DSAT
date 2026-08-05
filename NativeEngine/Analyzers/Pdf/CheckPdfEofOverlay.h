#ifndef CHECK_PDF_EOF_OVERLAY_H
#define CHECK_PDF_EOF_OVERLAY_H

#include "../DocumentAnalyzerBase.h"
#include <cstdio>
#include <cstdint>

// PDF %PDF- 헤더 검증 및 %%EOF 탐색 기반 논리적 크기 / 오버레이 탐지
DetectionFinding CheckPdfEofOverlay(FILE* fp, uint64_t fileSize, uint64_t& outLogicalSize);

#endif /* CHECK_PDF_EOF_OVERLAY_H */
