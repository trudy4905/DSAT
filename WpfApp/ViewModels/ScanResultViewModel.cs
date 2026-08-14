using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfApp.Models;
using WpfApp.Services;
using WpfApp.Views;

namespace WpfApp.ViewModels
{
    public class ScanResultViewModel : ObservableObject
    {
        private string _searchQuery = string.Empty;
        private string _currentSortColumn = "StatusText";
        private bool _isSortAscending = false;
        private HwpFileItem? _selectedFile;

        private bool _isPreviewLoading;
        private string _previewTitle = "파일을 선택하세요";
        private string _previewFilePath = string.Empty;
        private string _previewFileSize = "-";
        private string _previewLastModified = "-";
        private string _previewFormatType = "-";
        private string _previewText = "왼쪽 목록에서 .hwp 또는 .hwpx 파일을 선택하면 여기에 내용이 표시됩니다.";
        private bool _isPdfPreview;
        private Uri? _previewUri;
        private int _previewLineCount = 0;
        private int _previewCharCount = 0;

        private string _previewHtmlContent = string.Empty;

        public bool IsPdfPreview
        {
            get => _isPdfPreview;
            set => SetProperty(ref _isPdfPreview, value);
        }

        public Uri? PreviewUri
        {
            get => _previewUri;
            set => SetProperty(ref _previewUri, value);
        }

        public string PreviewHtmlContent
        {
            get => _previewHtmlContent;
            set => SetProperty(ref _previewHtmlContent, value);
        }

        private DiskItem? _selectedDisk;
        private bool _hasNoScanResult;
        private string _scanProgressText = string.Empty;

        public ObservableCollection<HwpFileItem> FileList { get; } = new ObservableCollection<HwpFileItem>();
        public ObservableCollection<HwpFileItem> FilteredFileList { get; } = new ObservableCollection<HwpFileItem>();

        public event EventHandler? RequestGoBack;
        public event EventHandler? RequestRefreshScan;

        public ScanResultViewModel()
        {
            SortColumnCommand = new RelayCommand(param =>
            {
                if (param is string colName)
                {
                    if (_currentSortColumn == colName)
                    {
                        _isSortAscending = !_isSortAscending;
                    }
                    else
                    {
                        _currentSortColumn = colName;
                        _isSortAscending = true;
                    }
                    ApplyFileFilter();
                    NotifyHeaderPropertiesChanged();
                }
            });

            OpenFileCommand = new RelayCommand(_ =>
            {
                if (SelectedFile == null) return;
                // 포렌식 이미지에서 추출된 경우 TempExtractPath, 일반 파일은 FilePath 사용
                string openPath = !string.IsNullOrEmpty(SelectedFile.TempExtractPath) && File.Exists(SelectedFile.TempExtractPath)
                    ? SelectedFile.TempExtractPath
                    : SelectedFile.FilePath;
                if (!File.Exists(openPath)) return;
                try
                {
                    // 실행 파일이 위치한 경로(USB) 내 DSAT_Temp 임시 폴더 이용
                    string exeDir = AppContext.BaseDirectory;
                    string tempDir = Path.Combine(exeDir, "DSAT_Temp");

                    try
                    {
                        Directory.CreateDirectory(tempDir);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                    {
                        MessageBox.Show("USB 드라이브에 쓰기 권한이 없습니다. (보안 매체 / USB 쓰기 금지 환경)\n\n실행 파일 경로에 임시 폴더를 생성할 수 없어 파일 열기를 진행할 수 없습니다.",
                            "쓰기 권한 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string safeFileName = $"SafeCopy_{Guid.NewGuid().ToString("N")[..8]}_{Path.GetFileName(openPath)}";
                    string tempCopyPath = Path.Combine(tempDir, safeFileName);

                    // 원본 무결성 보존을 위해 임시 복사본 생성 후 읽기 전용 설정
                    File.Copy(openPath, tempCopyPath, overwrite: true);
                    File.SetAttributes(tempCopyPath, FileAttributes.ReadOnly);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tempCopyPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"파일 열기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }, _ => SelectedFile != null && (!string.IsNullOrEmpty(SelectedFile?.TempExtractPath) && File.Exists(SelectedFile?.TempExtractPath) || File.Exists(SelectedFile?.FilePath ?? string.Empty)));

            OpenFolderCommand = new RelayCommand(_ =>
            {
                if (SelectedFile == null) return;
                string openPath = !string.IsNullOrEmpty(SelectedFile.TempExtractPath) && File.Exists(SelectedFile.TempExtractPath)
                    ? SelectedFile.TempExtractPath
                    : SelectedFile.FilePath;
                if (!File.Exists(openPath)) return;
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{openPath}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"폴더 열기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }, _ => SelectedFile != null && (!string.IsNullOrEmpty(SelectedFile?.TempExtractPath) && File.Exists(SelectedFile?.TempExtractPath) || File.Exists(SelectedFile?.FilePath ?? string.Empty)));

            CopyTextCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrEmpty(PreviewText))
                {
                    Clipboard.SetText(PreviewText);
                    MessageBox.Show("미리보기 텍스트가 클립보드에 복사되었습니다.", "복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }, _ => !string.IsNullOrEmpty(PreviewText));

            GoToPreviousStepCommand = new RelayCommand(_ => RequestGoBack?.Invoke(this, EventArgs.Empty));
            RefreshScanCommand = new RelayCommand(_ => RequestRefreshScan?.Invoke(this, EventArgs.Empty));

            ExportSelectedFilesCommand = new RelayCommand(_ =>
            {
                // TempExtractPath(포렌식 추출 파일) 또는 FilePath(일반 파일) 존재하는 파일만 포함
                var targetFiles = FilteredFileList.Where(x => x.IsSelectedForExport &&
                    ((!string.IsNullOrEmpty(x.TempExtractPath) && File.Exists(x.TempExtractPath)) || File.Exists(x.FilePath))).ToList();
                if (targetFiles.Count == 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dialog = new ExportCompleteDialog(0, string.Empty)
                        {
                            Owner = Application.Current.MainWindow
                        };
                        dialog.ShowDialog();
                    });
                    return;
                }

                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "내보낼 저장 폴더 선택"
                };

                if (dialog.ShowDialog() == true)
                {
                    string targetFolder = dialog.FolderName;
                    int successCount = 0;
                    int failCount = 0;

                    foreach (var file in targetFiles)
                    {
                        try
                        {
                            // 실제 파일 경로: TempExtractPath(포렌식 추출) 우선, 없으면 FilePath
                            string sourcePath = !string.IsNullOrEmpty(file.TempExtractPath) && File.Exists(file.TempExtractPath)
                                ? file.TempExtractPath
                                : file.FilePath;

                            string destFileName = file.FileName;
                            string destPath = Path.Combine(targetFolder, destFileName);

                            int counter = 1;
                            string fileNoExt = Path.GetFileNameWithoutExtension(destFileName);
                            string ext = Path.GetExtension(destFileName);

                            while (File.Exists(destPath))
                            {
                                destPath = Path.Combine(targetFolder, $"{fileNoExt}_{counter++}{ext}");
                            }

                            File.Copy(sourcePath, destPath, overwrite: true);
                            successCount++;
                        }
                        catch
                        {
                            failCount++;
                        }
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dialog = new ExportCompleteDialog(successCount, targetFolder)
                        {
                            Owner = Application.Current.MainWindow
                        };
                        dialog.ShowDialog();
                    });
                }
            });
        }

        public string SelectedExportCountText
        {
            get
            {
                int total = FilteredFileList.Count;
                int selected = FilteredFileList.Count(x => x.IsSelectedForExport);
                return $"선택된 문서: {selected:N0} / {total:N0}개";
            }
        }

        public bool? IsAllSelectedForExport
        {
            get
            {
                if (FilteredFileList.Count == 0) return false;
                int count = FilteredFileList.Count(x => x.IsSelectedForExport);
                if (count == FilteredFileList.Count) return true;
                if (count == 0) return false;
                return null;
            }
            set
            {
                if (value.HasValue)
                {
                    foreach (var item in FilteredFileList)
                    {
                        item.IsSelectedForExport = value.Value;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedExportCountText));
                }
            }
        }

        #region Commands
        public ICommand SortColumnCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand CopyTextCommand { get; }
        public ICommand GoToPreviousStepCommand { get; }
        public ICommand RefreshScanCommand { get; }
        public ICommand ExportSelectedFilesCommand { get; }
        #endregion

        #region Search and Filtering
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    ApplyFileFilter();
                }
            }
        }

        public void InitializeResults(IEnumerable<HwpFileItem> results)
        {
            var list = results.ToList();
            
            // Check if FileList already has the exact same items (from live streaming)
            bool needsRebuild = FileList.Count != list.Count;
            if (!needsRebuild)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (FileList[i] != list[i])
                    {
                        needsRebuild = true;
                        break;
                    }
                }
            }

            if (needsRebuild)
            {
                foreach (var item in FileList)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                }

                FileList.Clear();
                foreach (var item in list)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                    FileList.Add(item);
                }
            }
            else
            {
                foreach (var item in FileList)
                {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            ApplyFileFilter();

            if (SelectedFile == null && FilteredFileList.Count > 0)
            {
                SelectedFile = FilteredFileList.First();
            }

            OnPropertyChanged(nameof(IsAllSelectedForExport));
            OnPropertyChanged(nameof(SelectedExportCountText));
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HwpFileItem.IsSelectedForExport))
            {
                OnPropertyChanged(nameof(IsAllSelectedForExport));
                OnPropertyChanged(nameof(SelectedExportCountText));
            }
        }

        public void SortByColumn(string sortMemberPath)
        {
            if (string.IsNullOrEmpty(sortMemberPath)) return;
            if (_currentSortColumn == sortMemberPath)
            {
                _isSortAscending = !_isSortAscending;
            }
            else
            {
                _currentSortColumn = sortMemberPath;
                _isSortAscending = true;
            }
            ApplyFileFilter();
            NotifyHeaderPropertiesChanged();
        }

        private void ApplyFileFilter()
        {
            var filtered = FileList.Where(item => MatchesSearchFilter(item, SearchQuery));

            IEnumerable<HwpFileItem> sorted = _currentSortColumn switch
            {
                "IsSelectedForExport" => _isSortAscending ? filtered.OrderByDescending(x => x.IsSelectedForExport).ThenBy(x => x.FileName) : filtered.OrderBy(x => x.IsSelectedForExport).ThenBy(x => x.FileName),
                "StatusText" => _isSortAscending ? filtered.OrderBy(x => x.RiskLevel).ThenBy(x => x.StatusText) : filtered.OrderByDescending(x => x.RiskLevel).ThenByDescending(x => x.StatusText),
                "FileSizeBytes" => _isSortAscending ? filtered.OrderBy(x => x.FileSizeBytes) : filtered.OrderByDescending(x => x.FileSizeBytes),
                "Extension" => _isSortAscending ? filtered.OrderBy(x => x.Extension) : filtered.OrderByDescending(x => x.Extension),
                "FilePath" => _isSortAscending ? filtered.OrderBy(x => x.FilePath) : filtered.OrderByDescending(x => x.FilePath),
                "CreatedTime" => _isSortAscending ? filtered.OrderBy(x => x.CreatedTime) : filtered.OrderByDescending(x => x.CreatedTime),
                "LastModified" => _isSortAscending ? filtered.OrderBy(x => x.LastModified) : filtered.OrderByDescending(x => x.LastModified),
                "TextSnippet" => _isSortAscending ? filtered.OrderBy(x => x.TextSnippet) : filtered.OrderByDescending(x => x.TextSnippet),
                _ => _isSortAscending ? filtered.OrderBy(x => x.FileName) : filtered.OrderByDescending(x => x.FileName)
            };

            FilteredFileList.Clear();
            int idx = 1;
            foreach (var item in sorted)
            {
                item.Index = idx++;
                FilteredFileList.Add(item);
            }

            if (SelectedFile == null || !FilteredFileList.Contains(SelectedFile))
            {
                SelectedFile = FilteredFileList.FirstOrDefault();
            }

            OnPropertyChanged(nameof(DocumentBreakdownText));
            OnPropertyChanged(nameof(HwpCount));
            OnPropertyChanged(nameof(HwpxCount));
            OnPropertyChanged(nameof(PdfCount));
            OnPropertyChanged(nameof(IsAllSelectedForExport));
            OnPropertyChanged(nameof(SelectedExportCountText));
        }

        public int HwpCount => FileList.Count(x => x.Extension.Equals(".hwp", StringComparison.OrdinalIgnoreCase));
        public int HwpxCount => FileList.Count(x => x.Extension.Equals(".hwpx", StringComparison.OrdinalIgnoreCase));
        public int PdfCount => FileList.Count(x => x.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase));
        public string DocumentBreakdownText => $"검색된 문서 목록 (총 {FileList.Count:N0}개: HWP {HwpCount:N0}개, HWPX {HwpxCount:N0}개, PDF {PdfCount:N0}개)";

        private bool MatchesSearchFilter(HwpFileItem item, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return item.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   item.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   item.Extension.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   item.TextSnippet.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        public DiskItem? SelectedDisk
        {
            get => _selectedDisk;
            set { SetProperty(ref _selectedDisk, value); }
        }

        public bool HasNoScanResult
        {
            get => _hasNoScanResult;
            set { SetProperty(ref _hasNoScanResult, value); }
        }

        public string ScanProgressText
        {
            get => _scanProgressText;
            set { SetProperty(ref _scanProgressText, value); }
        }
        #endregion

        #region Sorting Headers
        private string GetSortArrow(string colName)
        {
            if (_currentSortColumn.Equals(colName, StringComparison.OrdinalIgnoreCase))
            {
                return _isSortAscending ? " ▲" : " ▼";
            }
            return string.Empty;
        }

        public string HeaderIndex => "번호";
        public string HeaderStatus => "분석 결과" + GetSortArrow("StatusText");
        public string HeaderFileName => "파일명" + GetSortArrow("FileName");
        public string HeaderFileSize => "크기" + GetSortArrow("FileSizeBytes");
        public string HeaderExtension => "확장자" + GetSortArrow("Extension");
        public string HeaderFilePath => "경로" + GetSortArrow("FilePath");
        public string HeaderCreatedTime => "만든시간" + GetSortArrow("CreatedTime");
        public string HeaderLastModified => "수정시간" + GetSortArrow("LastModified");
        public string HeaderTextSnippet => "본문 미리보기" + GetSortArrow("TextSnippet");

        private void NotifyHeaderPropertiesChanged()
        {
            OnPropertyChanged(nameof(HeaderIndex));
            OnPropertyChanged(nameof(HeaderStatus));
            OnPropertyChanged(nameof(HeaderFileName));
            OnPropertyChanged(nameof(HeaderFileSize));
            OnPropertyChanged(nameof(HeaderExtension));
            OnPropertyChanged(nameof(HeaderFilePath));
            OnPropertyChanged(nameof(HeaderCreatedTime));
            OnPropertyChanged(nameof(HeaderLastModified));
            OnPropertyChanged(nameof(HeaderTextSnippet));
        }
        #endregion

        #region Preview Logic
        public HwpFileItem? SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (_selectedFile != value)
                {
                    if (_selectedFile != null) _selectedFile.IsSelected = false;
                    _selectedFile = value;
                    if (_selectedFile != null) _selectedFile.IsSelected = true;

                    OnPropertyChanged();
                    (OpenFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (OpenFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CopyTextCommand as RelayCommand)?.RaiseCanExecuteChanged();

                    if (_selectedFile != null)
                    {
                        _ = LoadFilePreviewAsync(_selectedFile);
                    }
                    else
                    {
                        ResetPreview();
                    }
                }
            }
        }

        private bool _isPreviewError;
        public bool IsPreviewError
        {
            get => _isPreviewError;
            set { SetProperty(ref _isPreviewError, value); }
        }

        private string _previewErrorMessage = string.Empty;
        public string PreviewErrorMessage
        {
            get => _previewErrorMessage;
            set { SetProperty(ref _previewErrorMessage, value); }
        }

        public void SetPreviewError(string message)
        {
            PreviewErrorMessage = message;
            IsPreviewError = true;
            IsPreviewLoading = false;
        }

        public bool IsPreviewLoading
        {
            get => _isPreviewLoading;
            set { SetProperty(ref _isPreviewLoading, value); }
        }

        public string PreviewTitle
        {
            get => _previewTitle;
            set { SetProperty(ref _previewTitle, value); }
        }

        public string PreviewFilePath
        {
            get => _previewFilePath;
            set { SetProperty(ref _previewFilePath, value); }
        }

        public string PreviewFileSize
        {
            get => _previewFileSize;
            set { SetProperty(ref _previewFileSize, value); }
        }

        public string PreviewLastModified
        {
            get => _previewLastModified;
            set { SetProperty(ref _previewLastModified, value); }
        }

        public string PreviewFormatType
        {
            get => _previewFormatType;
            set { SetProperty(ref _previewFormatType, value); }
        }

        public string PreviewText
        {
            get => _previewText;
            set
            {
                if (SetProperty(ref _previewText, value))
                {
                    (CopyTextCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public int PreviewLineCount
        {
            get => _previewLineCount;
            set { SetProperty(ref _previewLineCount, value); }
        }

        public int PreviewCharCount
        {
            get => _previewCharCount;
            set { SetProperty(ref _previewCharCount, value); }
        }

        private async Task LoadFilePreviewAsync(HwpFileItem fileItem)
        {
            IsPreviewError = false;
            IsPreviewLoading = true;
            PreviewTitle = fileItem.FileName;
            PreviewFilePath = fileItem.FilePath;
            PreviewFileSize = fileItem.FileSizeFormatted;
            PreviewLastModified = fileItem.LastModifiedFormatted;

            string targetPath = !string.IsNullOrEmpty(fileItem.TempExtractPath) && File.Exists(fileItem.TempExtractPath)
                ? fileItem.TempExtractPath
                : fileItem.FilePath;

            if (fileItem.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                IsPdfPreview = true;
                PreviewFormatType = "PDF";
                try
                {
                    PreviewUri = new Uri(targetPath);
                }
                catch { }
                IsPreviewLoading = false;
                return;
            }

            IsPdfPreview = false;
            PreviewText = "본문 텍스트 추출 중...";

            try
            {
                var result = await DocumentPreviewService.ExtractTextAsync(targetPath);

                PreviewFormatType = result.FormatType;
                PreviewText = result.ContentText;
                PreviewLineCount = result.LineCount;
                PreviewCharCount = result.CharCount;

                string html = await Task.Run(() => HwpHtmlDocumentGenerator.GenerateHwpHtmlDocument(
                    fileItem.FileName, 
                    result.ContentText, 
                    result.FormatType, 
                    fileItem.FilePath, 
                    fileItem.FileSizeFormatted, 
                    fileItem.LastModifiedFormatted
                ));

                PreviewHtmlContent = html;
            }
            catch (Exception ex)
            {
                PreviewFormatType = "Error";
                PreviewText = $"파일 미리보기를 읽는 중 오류가 발생했습니다.\n\n{ex.Message}";
                PreviewLineCount = 0;
                PreviewCharCount = 0;
            }
            finally
            {
                IsPreviewLoading = false;
            }
        }

        private void ResetPreview()
        {
            PreviewTitle = "파일을 선택하세요";
            PreviewFilePath = string.Empty;
            PreviewFileSize = "-";
            PreviewLastModified = "-";
            PreviewFormatType = "-";
            PreviewText = "왼쪽 목록에서 .hwp 또는 .hwpx 파일을 선택하면 여기에 내용이 표시됩니다.";
            PreviewLineCount = 0;
            PreviewCharCount = 0;
            IsPreviewLoading = false;
        }
        #endregion
    }
}
