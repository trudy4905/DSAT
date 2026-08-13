#ifndef CHECK_HWP_OLE_SLACK_H
#define CHECK_HWP_OLE_SLACK_H

#include "../DocumentAnalyzerBase.h"
#include <cstdio>
#include <cstdint>

DetectionFinding CheckHwpOleSlack(FILE *fp, uint64_t fileSize);

#endif // CHECK_HWP_OLE_SLACK_H
