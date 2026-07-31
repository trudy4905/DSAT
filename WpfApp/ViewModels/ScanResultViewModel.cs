using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfApp.Models;
using WpfApp.Services;

namespace WpfApp.ViewModels
{
    public class ScanResultViewModel : ObservableObject
    {
        private string _searchQuery = string.Empty;
        private string _currentSortColumn = "FileName";
        private bool _isSortAscending = true;
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
                if (SelectedFile != null && File.Exists(SelectedFile.FilePath))
                {
                    try
                    {
                        // Create safe temp directory for forensic replica viewing
                        string tempDir = Path.Combine(Path.GetTempPath(), "DSAT_SafeReplica");
                        Directory.CreateDirectory(tempDir);

                        string safeFileName = $"SafeCopy_{Guid.NewGuid().ToString("N")[..8]}_{Path.GetFileName(SelectedFile.FilePath)}";
                        string tempCopyPath = Path.Combine(tempDir, safeFileName);

                        // Copy original file to temp directory (Preserves original evidence metadata)
                        File.Copy(SelectedFile.FilePath, tempCopyPath, overwrite: true);

                        // Set ReadOnly attribute on temp copy
                        File.SetAttributes(tempCopyPath, FileAttributes.ReadOnly);

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = tempCopyPath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"안전 복사본 파일 열기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }, _ => SelectedFile != null && File.Exists(SelectedFile?.FilePath));

            OpenFolderCommand = new RelayCommand(_ =>
            {
                if (SelectedFile != null && File.Exists(SelectedFile.FilePath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{SelectedFile.FilePath}\"",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"폴더 열기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }, _ => SelectedFile != null && File.Exists(SelectedFile?.FilePath));

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
        }

        #region Commands
        public ICommand SortColumnCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand CopyTextCommand { get; }
        public ICommand GoToPreviousStepCommand { get; }
        public ICommand RefreshScanCommand { get; }
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
            FileList.Clear();
            foreach (var item in results)
            {
                FileList.Add(item);
            }
            ApplyFileFilter();

            if (FilteredFileList.Count > 0)
            {
                SelectedFile = FilteredFileList.First();
            }
        }

        private void ApplyFileFilter()
        {
            var filtered = FileList.Where(item => MatchesSearchFilter(item, SearchQuery));

            IEnumerable<HwpFileItem> sorted = _currentSortColumn switch
            {
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
            IsPreviewLoading = true;
            PreviewTitle = fileItem.FileName;
            PreviewFilePath = fileItem.FilePath;
            PreviewFileSize = fileItem.FileSizeFormatted;
            PreviewLastModified = fileItem.LastModifiedFormatted;

            if (fileItem.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                IsPdfPreview = true;
                PreviewFormatType = "PDF";
                try
                {
                    PreviewUri = new Uri(fileItem.FilePath);
                }
                catch { }
                IsPreviewLoading = false;
                return;
            }

            IsPdfPreview = false;
            PreviewText = "본문 텍스트 추출 중...";

            try
            {
                var result = await DocumentPreviewService.ExtractTextAsync(fileItem.FilePath);

                PreviewFormatType = result.FormatType;
                PreviewText = result.ContentText;
                PreviewLineCount = result.LineCount;
                PreviewCharCount = result.CharCount;

                PreviewHtmlContent = HwpHtmlDocumentGenerator.GenerateHwpHtmlDocument(
                    fileItem.FileName, 
                    result.ContentText, 
                    result.FormatType, 
                    fileItem.FilePath, 
                    fileItem.FileSizeFormatted, 
                    fileItem.LastModifiedFormatted
                );
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
