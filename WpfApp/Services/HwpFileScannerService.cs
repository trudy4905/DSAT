using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WpfApp.Models;

namespace WpfApp.Services
{
    public class HwpFileScannerService
    {
        private static readonly HashSet<string> SkipFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "$Recycle.Bin", "System Volume Information", "Windows", "Program Files", "Program Files (x86)", "AppData", "ProgramData"
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

        #region Direct File List Scanner (멀티 선택 파일 검사)
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

                count++;
                var file = new FileInfo(path);
                string ext = file.Extension.ToLowerInvariant();

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

                // C++ 네이티브 엔진 호출
                try
                {
                    int res = NativeBridge.Engine_AnalyzeDocumentOverlay(file.FullName, out var analysis);
                    if (res != 0)
                    {
                        item.IsNormal = analysis.IsNormal == 1;
                        item.HasOverlay = analysis.HasOverlay == 1;
                        item.OverlaySizeBytes = (long)analysis.OverlaySize;

                        var findings = new List<DetectionFindingItem>();

                        if (analysis.HasOverlay == 1)
                        {
                            double kb = analysis.OverlaySize / 1024.0;
                            string overlayStr = kb >= 1024.0 ? $"{kb / 1024.0:F2} MB" : $"{kb:F1} KB";

                            findings.Add(new DetectionFindingItem
                            {
                                Title = "EOF 은닉 데이터 발견 (Overlay Data)",
                                Description = $"정식 문서 구조 마감 오프셋 뒤에 {overlayStr} 크기의 비인가 잉여 오버레이 바이너리가 은닉되어 있습니다.",
                                RiskLevel = 2
                            });
                        }

                        item.Findings = findings;

                        if (findings.Count > 1)
                        {
                            item.RiskLevel = 3;
                            item.StatusText = $"복합 위험 ({findings.Count}건)";
                        }
                        else if (findings.Count == 1)
                        {
                            item.RiskLevel = findings[0].RiskLevel;
                            item.StatusText = analysis.StatusMessage;
                        }
                        else
                        {
                            item.RiskLevel = 0;
                            item.StatusText = "정상";
                        }
                    }
                }
                catch
                {
                    item.StatusText = "검사 실패";
                    item.RiskLevel = 1;
                }

                // 비동기 미리보기 텍스트 추출
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (ext == ".pdf")
                        {
                            item.TextSnippet = "(PDF 바이너리 문서 분석 완료)";
                        }
                        else
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
                    }
                    catch
                    {
                        item.TextSnippet = "(본문 요약 실패)";
                    }
                });

                resultList.Add(item);
                progress?.Report((count, Path.GetDirectoryName(path) ?? string.Empty, item));
                await Task.Yield();
            }

            return resultList;
        }
        #endregion

        #region Image File Scanner
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
                progress?.Report((scannedDirs, $"{partitionTag} - 파티션 레코드 파싱 완료", null));
                await Task.Delay(300, cancellationToken);

                progress?.Report((scannedDirs, $"{partitionTag} - 탐색 완료", null));
            }

            return resultList;
        }
        #endregion

        #region Local Physical Disk Directory Scanner
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
                        if (ext == ".hwp" || ext == ".hwpx" || ext == ".pdf")
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

                            // C++ 네이티브 엔진 호출: 다중 위협 & EOF 은닉 데이터 파싱
                            try
                            {
                                int res = NativeBridge.Engine_AnalyzeDocumentOverlay(file.FullName, out var analysis);
                                if (res != 0)
                                {
                                    item.IsNormal = analysis.IsNormal == 1;
                                    item.HasOverlay = analysis.HasOverlay == 1;
                                    item.OverlaySizeBytes = (long)analysis.OverlaySize;

                                    var findings = new List<DetectionFindingItem>();

                                    if (analysis.HasOverlay == 1)
                                    {
                                        double kb = analysis.OverlaySize / 1024.0;
                                        string overlayStr = kb >= 1024.0 ? $"{kb / 1024.0:F2} MB" : $"{kb:F1} KB";

                                        findings.Add(new DetectionFindingItem
                                        {
                                            Title = "EOF 은닉 데이터 발견 (Overlay Data)",
                                            Description = $"정식 문서 구조 마감 오프셋 뒤에 {overlayStr} 크기의 비인가 잉여 오버레이 바이너리가 은닉되어 있습니다.",
                                            RiskLevel = 2 // Danger (Red)
                                        });
                                    }

                                    item.Findings = findings;

                                    if (findings.Count > 1)
                                    {
                                        item.RiskLevel = 3; // Critical (Purple)
                                        item.StatusText = $"복합 위험 ({findings.Count}건)";
                                    }
                                    else if (findings.Count == 1)
                                    {
                                        item.RiskLevel = findings[0].RiskLevel;
                                        item.StatusText = analysis.StatusMessage;
                                    }
                                    else
                                    {
                                        item.RiskLevel = 0; // Safe (Green)
                                        item.StatusText = "정상";
                                    }
                                }
                            }
                            catch
                            {
                                item.StatusText = "검사 실패";
                                item.RiskLevel = 1; // Caution
                            }

                            // 비동기 텍스트 미리보기 가공 (HWP / HWPX 전용)
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    if (ext == ".pdf")
                                    {
                                        item.TextSnippet = "(PDF 바이너리 문서 분석 완료)";
                                    }
                                    else
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
                                }
                                catch
                                {
                                    item.TextSnippet = "(본문 요약 실패)";
                                }
                            });

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
    }
}
