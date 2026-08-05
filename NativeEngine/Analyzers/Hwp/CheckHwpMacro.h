#ifndef CHECK_HWP_MACRO_H
#define CHECK_HWP_MACRO_H

#include "../DocumentAnalyzerBase.h"
#include <cstdio>
#include <cstdint>

// HWP 문서 내 VBA/JScript 매크로 및 외부 명령 실행 코드 탐지 모듈
DetectionFinding CheckHwpMacro(FILE* fp, uint64_t fileSize);

#endif /* CHECK_HWP_MACRO_H */
