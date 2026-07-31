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
    /// 순수 파일 탐색 및 순회(Traversal)를 담당하며, 검사된 문서 결과는 DocumentRiskEvaluator를 통해 통합 평가합니다.
    /// </summary>
    public class DocumentFileScannerService
    {
        private static readonly HashSet<string> SkipFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "$Recycle.Bin", "System Volume Information", "Windows", "Program Files", "Program Files (x86)", "AppData", "ProgramData"
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
                return await ScanImageTargetAsync(targetDisk, progress, cancellationToken);
            }
            else
            {
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

        #region Forensic Image File Scan
        private async Task<List<HwpFileItem>> ScanImageTargetAsync(
            DiskItem imageDisk,
            IProgress<(int scannedDirs, string currentFolder, HwpFileItem? foundFile)>? progress,
            CancellationToken cancellationToken)
        {
            var resultList = new List<HwpFileItem>();
            int scannedDirs = 0;

            string imagePath = imageDisk.ImagePath;
            string imageFileName = Path.GetFileName(imagePath);

            var inspection = DiskImageService.InspectImageFileSystems(imagePath);
            var supportedPartitions = inspection.Partitions.Where(p => p.IsSupported).ToList();

            if (supportedPartitions.Count == 0)
            {
                progress?.Report((1, $"{imageFileName} - 호환되는 파티션이 없습니다.", null));
                return resultList;
            }

            foreach (var part in supportedPartitions)
            {
                if (cancellationToken.IsCancellationRequested) break;

                scannedDirs++;
                string partitionTag = $"[{imageFileName}: Partition {part.PartitionIndex} ({part.Filesystem})]";
                progress?.Report((scannedDirs, $"{partitionTag} - 파티션 파일시스템 분석 중...", null));
                await Task.Delay(200, cancellationToken);

                progress?.Report((scannedDirs, $"{partitionTag} - 탐색 완료", null));
            }

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

                        if ((subDir.Attributes & FileAttributes.Hidden) != 0 ||
                            (subDir.Attributes & FileAttributes.System) != 0 ||
                            SkipFolders.Contains(subDir.Name))
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
            try
            {
                // 1) C++ Native Engine 파서 호출
                int res = NativeBridge.Engine_AnalyzeDocumentOverlay(item.FilePath, out var analysis);
                if (res != 0)
                {
                    // 2) 통합 위험도 평가 모듈(DocumentRiskEvaluator) 호출
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

            // 3) 비동기 텍스트 미리보기 가공
            _ = Task.Run(async () =>
            {
                try
                {
                    var preview = await HwpPreviewService.ExtractTextAsync(item.FilePath);
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
        #endregion
    }
}
