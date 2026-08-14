using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WpfApp.Models;
using WpfApp.Services.Evaluators;

namespace WpfApp.Services
{
    /// <summary>
    /// 디스크 및 이미지 파일 탐색기 (Document File System & Image Partition Scanner)
    /// Native Engine(libewf/libtsk)을 활용하여 이미지 내 파티션 및 문서 파일들을 100% 추출/분석합니다.
    /// </summary>
    public class DocumentFileScannerService
    {
        private static readonly HashSet<string> SkipFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Windows", "Program Files", "Program Files (x86)", "System Volume Information"
        };

        private static readonly HashSet<string> TargetExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".hwp", ".hwpx", ".pdf"
        };

        public async Task<List<HwpFileItem>> ScanTargetAsync(
            DiskItem targetDisk,
            IProgress<(int scannedDirs, string currentFolder, HwpFileItem? foundFile)>? progress,
            CancellationToken cancellationToken)
        {
            if (targetDisk.IsDirectFilesMode)
            {
                return await ScanDirectFilesAsync(targetDisk.DirectFilePaths, progress, cancellationToken);
            }
            else if (targetDisk.IsImageFile)
            {
                return await ScanImageTargetAsync(targetDisk, false, progress, cancellationToken);
            }
            else
            {
                // Standard Live File System Scan (Fast OS Directory Traversal)
                string targetDir = targetDisk.DriveLetter;
                if (!Directory.Exists(targetDir))
                {
                    targetDir = Path.GetPathRoot(targetDisk.DriveLetter) ?? targetDisk.DriveLetter;
                }

                return await ScanLocalDirectoryAsync(targetDir, progress, cancellationToken);
            }
        }

        #region Direct Selected Files Scan
        private async Task<List<HwpFileItem>> ScanDirectFilesAsync(
            List<string> filePaths,
            IProgress<(int scannedDirs, string currentFolder, HwpFileItem? foundFile)>? progress,
            CancellationToken cancellationToken)
        {
            var resultList = new List<HwpFileItem>();
            int count = 0;

            foreach (var path in filePaths)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!File.Exists(path)) continue;

                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (!TargetExtensions.Contains(ext)) continue;

                count++;
                var file = new FileInfo(path);

                var item = new HwpFileItem
                {
                    FileName = file.Name,
                    FilePath = file.FullName,
                    Extension = ext,
                    FileSizeBytes = file.Length,
                    CreatedTime = file.CreationTime,
                    LastModified = file.LastWriteTime,
                    TextSnippet = "분석 중..."
                };

                AnalyzeAndEvaluateFile(item);

                resultList.Add(item);
                progress?.Report((count, Path.GetDirectoryName(path) ?? string.Empty, item));
            }

            return resultList;
        }
        #endregion

        #region Forensic Image File Scan (Native libewf / libtsk Engine)
        private async Task<List<HwpFileItem>> ScanImageTargetAsync(
            DiskItem imageDisk,
            bool includeDeleted,
            IProgress<(int scannedDirs, string currentFolder, HwpFileItem? foundFile)>? progress,
            CancellationToken cancellationToken)
        {
            var resultList = new List<HwpFileItem>();

            string imagePath = imageDisk.ImagePath;

            string tempExtractDir = Path.Combine(Path.GetTempPath(), "DSAT_Forensic_Extracted", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempExtractDir);

            progress?.Report((1, "Native C++ 포렌식 디스크 엔진 스캔 시작...", null));

            await Task.Run(() =>
            {
                // C++에서: currentPath = 실제 임시 추출 경로, statusMsg = "FILE:IS_DELETED:1:가상경로"
                ImageScanProgressCallbackDelegate cb = (scannedCount, currentPath, statusMsg) =>
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    if (scannedCount < 0 || statusMsg == "SCANNING_DIR")
                    {
                        // 디렉토리 탐색 진행 상황
                        string folderPath = string.IsNullOrEmpty(currentPath)
                            ? "\\"
                            : (currentPath.StartsWith("\\") || currentPath.StartsWith("/") ? currentPath.Replace('/', '\\') : $"\\{currentPath.Replace('/', '\\')}");
                        progress?.Report((-1, folderPath, null));
                        return;
                    }

                    // statusMsg가 "FILE:" 형식인지 확인
                    string virtualPath = string.Empty;
                    if (statusMsg != null && statusMsg.StartsWith("FILE:", StringComparison.Ordinal))
                    {
                        string payload = statusMsg.Substring(5);
                        if (payload.StartsWith("IS_DELETED:1:", StringComparison.Ordinal))
                        {
                            virtualPath = payload.Substring(13);
                        }
                        else if (payload.StartsWith("IS_DELETED:0:", StringComparison.Ordinal))
                        {
                            virtualPath = payload.Substring(13);
                        }
                        else
                        {
                            virtualPath = payload;
                        }
                    }

                    // currentPath = 실제 temp 파일 경로 → FileInfo로 파일 크기/날짜 읽기
                    string realTempPath = currentPath;
                    string ext = Path.GetExtension(virtualPath.Length > 0 ? virtualPath : realTempPath).ToLowerInvariant();

                    // 원본 파일명: 가상 경로에서 추출 (한글 파일명 포함)
                    string originalFileName = virtualPath.Length > 0
                        ? Path.GetFileName(virtualPath)
                        : Path.GetFileName(realTempPath);

                    long fileSize = 0;
                    DateTime cTime = DateTime.MinValue;
                    DateTime mTime = DateTime.MinValue;

                    try
                    {
                        var fi = new FileInfo(realTempPath);
                        if (fi.Exists)
                        {
                            fileSize = fi.Length;
                            cTime = fi.CreationTime;
                            mTime = fi.LastWriteTime;
                        }
                    }
                    catch { }

                    var item = new HwpFileItem
                    {
                        FileName = originalFileName,
                        FilePath = realTempPath,       // 분석용 실제 경로 (나중에 UI 경로로 교체)
                        TempExtractPath = realTempPath, // 파일 열기/내보내기에 사용
                        Extension = ext,
                        FileSizeBytes = fileSize,
                        CreatedTime = cTime == DateTime.MinValue ? DateTime.Now : cTime,
                        LastModified = mTime == DateTime.MinValue ? DateTime.Now : mTime,
                        TextSnippet = "분석 중...",
                        VirtualPath = virtualPath.Length > 0 ? virtualPath : string.Empty
                    };

                    // 실제 temp 경로로 문서 오버레이 및 악성 여부 분석 수행
                    AnalyzeAndEvaluateFile(item);

                    // UI 표시 경로: 이미지 내부 가상 경로 형식 (예: \문서\보고서.hwp)
                    string displayVirtual = string.IsNullOrEmpty(item.VirtualPath)
                        ? item.FileName
                        : item.VirtualPath.TrimStart('/', '\\').Replace('/', '\\');
                    item.FilePath = displayVirtual.StartsWith("\\") ? displayVirtual : $"\\{displayVirtual}";

                    lock (resultList)
                    {
                        resultList.Add(item);
                    }

                    progress?.Report((scannedCount, $"{item.FileName} 분석 완료", item));
                };

                int includeDeletedFlag = includeDeleted ? 1 : 0;
                NativeBridge.Engine_ExtractDocumentFilesFromImage(imagePath, tempExtractDir, includeDeletedFlag, cb, out var outCount);
            }, cancellationToken);

            return resultList;
        }
        #endregion

        #region Local Directory Scan
        private async Task<List<HwpFileItem>> ScanLocalDirectoryAsync(
            string startDirectory,
            IProgress<(int scannedDirs, string currentFolder, HwpFileItem? foundFile)>? progress,
            CancellationToken cancellationToken)
        {
            var resultList = new List<HwpFileItem>();
            var dirQueue = new Queue<string>();
            string execDriveRoot = DiskService.GetExecutionDriveRoot();

            if (!string.IsNullOrEmpty(execDriveRoot) &&
                startDirectory.StartsWith(execDriveRoot, StringComparison.OrdinalIgnoreCase))
            {
                // 본인 USB 실행 드라이브 스캔 차단 (현장용 무흔적 원칙)
                return resultList;
            }

            if (Directory.Exists(startDirectory))
            {
                dirQueue.Enqueue(startDirectory);
            }

            int scannedDirs = 0;

            while (dirQueue.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested) break;

                string currentDir = dirQueue.Dequeue();
                scannedDirs++;

                progress?.Report((scannedDirs, currentDir, null));

                try
                {
                    var dirInfo = new DirectoryInfo(currentDir);
                    FileInfo[] files = dirInfo.GetFiles();

                    foreach (var file in files)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        string ext = file.Extension.ToLowerInvariant();
                        if (TargetExtensions.Contains(ext))
                        {
                            var item = new HwpFileItem
                            {
                                FileName = file.Name,
                                FilePath = file.FullName,
                                Extension = ext,
                                FileSizeBytes = file.Length,
                                CreatedTime = file.CreationTime,
                                LastModified = file.LastWriteTime,
                                TextSnippet = "분석 중..."
                            };

                            AnalyzeAndEvaluateFile(item);

                            resultList.Add(item);
                            progress?.Report((scannedDirs, currentDir, item));
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
                catch (Exception) { }

                try
                {
                    var dirInfo = new DirectoryInfo(currentDir);
                    DirectoryInfo[] subDirs = dirInfo.GetDirectories();

                    foreach (var subDir in subDirs)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0 ||
                            SkipFolders.Contains(subDir.Name) ||
                            (!string.IsNullOrEmpty(execDriveRoot) && subDir.FullName.StartsWith(execDriveRoot, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        dirQueue.Enqueue(subDir.FullName);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
                catch (Exception) { }

                await Task.Yield();
            }

            return resultList;
        }
        #endregion

        #region Helper: C++ Native Analysis & Risk Evaluation
        private static void AnalyzeAndEvaluateFile(HwpFileItem item)
        {
            string realPath = !string.IsNullOrEmpty(item.TempExtractPath) && File.Exists(item.TempExtractPath)
                ? item.TempExtractPath
                : item.FilePath;

            try
            {
                int res = NativeBridge.Engine_AnalyzeDocumentOverlay(realPath, out var analysis);
                if (res != 0)
                {
                    DocumentRiskEvaluator.EvaluateDocument(item, analysis);
                }
                else
                {
                    item.StatusText = "실패";
                    item.RiskLevel = 1;
                }
            }
            catch
            {
                item.StatusText = "검사 오류";
                item.RiskLevel = 1;
            }

            bool isHwpFile = item.Extension.Equals(".hwp", StringComparison.OrdinalIgnoreCase) ||
                             item.Extension.Equals(".hwpx", StringComparison.OrdinalIgnoreCase);

            if (isHwpFile)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var preview = await DocumentPreviewService.ExtractTextAsync(realPath);
                        if (preview.Success && !string.IsNullOrWhiteSpace(preview.ContentText))
                        {
                            string cleanText = preview.ContentText.Replace("\r", " ").Replace("\n", " ");
                            item.TextSnippet = cleanText.Length > 80 ? cleanText.Substring(0, 80) + "..." : cleanText;
                        }
                        else
                        {
                            item.TextSnippet = "(본문 내용 없음)";
                        }
                    }
                    catch
                    {
                        item.TextSnippet = "(본문 요약 실패)";
                    }
                });
            }
            else
            {
                item.TextSnippet = "- ";
            }
        }
        #endregion
    }
}
