#ifndef CHECK_HWP_STRUCTURE_H
#define CHECK_HWP_STRUCTURE_H

#include "../DocumentAnalyzerBase.h"
#include <cstdio>
#include <cstdint>

// HWP Header 및 OLE 엔트리 무결성/변조 탐지 모듈
DetectionFinding CheckHwpStructure(FILE* fp, uint64_t fileSize);

#endif /* CHECK_HWP_STRUCTURE_H */
