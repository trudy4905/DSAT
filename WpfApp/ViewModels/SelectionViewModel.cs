using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WpfApp.Models;
using WpfApp.Services;

namespace WpfApp.ViewModels
{
    public class SelectionViewModel : ObservableObject
    {
        private int _selectedSidebarTab = 0; // 0 = 디스크 장치, 1 = 이미지 파일, 2 = 파일 선택
        private DiskItem? _selectedDisk;

        public ObservableCollection<DiskItem> Disks { get; } = new ObservableCollection<DiskItem>();
        public ObservableCollection<DiskItem> DiskDevicesList { get; } = new ObservableCollection<DiskItem>();
        public ObservableCollection<DiskItem> ImageFilesList { get; } = new ObservableCollection<DiskItem>();

        // 파일 선택 탭용 선택된 파일 목록
        public ObservableCollection<SelectedFileItem> SelectedFiles { get; } = new ObservableCollection<SelectedFileItem>();

        public event EventHandler<DiskItem>? ScanRequested;

        public SelectionViewModel()
        {
            SelectSidebarTabCommand = new RelayCommand(param =>
            {
                if (param is string tabIndexStr && int.TryParse(tabIndexStr, out int tabIndex))
                {
                    SelectedSidebarTab = tabIndex;
                }
            });

            SelectDiskCardCommand = new RelayCommand(param =>
            {
                if (param is DiskItem disk)
                {
                    SelectedDisk = disk;
                }
            });

            RefreshDisksCommand = new RelayCommand(_ => LoadDisks());

            AddFilesCommand = new RelayCommand(_ => AddFiles());
            RemoveFileCommand = new RelayCommand(param =>
            {
                if (param is SelectedFileItem item)
                {
                    RemoveFile(item);
                }
            });
            ClearAllFilesCommand = new RelayCommand(_ => ClearAllFiles());

            GoToNextStepCommand = new RelayCommand(_ =>
            {
                if (SelectedSidebarTab == 2)
                {
                    var allFiles = SelectedFiles.Select(x => x.FilePath).ToList();
                    var diskItem = new DiskItem
                    {
                        IsDirectFilesMode = true,
                        DirectFilePaths = allFiles,
                        VolumeLabel = $"직접 선택 파일 ({TotalFileCount}개)",
                        DiskIndexStr = "직접 선택"
                    };
                    ScanRequested?.Invoke(this, diskItem);
                }
                else if (SelectedDisk != null && !SelectedDisk.IsAddCard)
                {
                    ScanRequested?.Invoke(this, SelectedDisk);
                }
            }, _ =>
            {
                if (SelectedSidebarTab == 2) return TotalFileCount > 0;
                return SelectedDisk != null && !SelectedDisk.IsAddCard;
            });

            LoadDisks();
        }

        #region Commands
        public ICommand SelectSidebarTabCommand { get; }
        public ICommand SelectDiskCardCommand { get; }
        public ICommand RefreshDisksCommand { get; }
        public ICommand GoToNextStepCommand { get; }
        public ICommand AddFilesCommand { get; }
        public ICommand RemoveFileCommand { get; }
        public ICommand ClearAllFilesCommand { get; }
        #endregion

        #region Properties
        public int SelectedSidebarTab
        {
            get => _selectedSidebarTab;
            set
            {
                if (SetProperty(ref _selectedSidebarTab, value))
                {
                    OnPropertyChanged(nameof(IsDiskDevicesTabActive));
                    OnPropertyChanged(nameof(IsImageFilesTabActive));
                    OnPropertyChanged(nameof(IsFileSelectionTabActive));
                    RaiseNextCanExecute();

                    if (IsDiskDevicesTabActive && DiskDevicesList.Count > 0)
                    {
                        SelectedDisk = DiskDevicesList.FirstOrDefault(d => !d.IsAddCard);
                    }
                    else if (IsImageFilesTabActive && ImageFilesList.Count > 0)
                    {
                        SelectedDisk = ImageFilesList.FirstOrDefault(d => !d.IsAddCard);
                    }
                }
            }
        }

        public bool IsDiskDevicesTabActive => SelectedSidebarTab == 0;
        public bool IsImageFilesTabActive => SelectedSidebarTab == 1;
        public bool IsFileSelectionTabActive => SelectedSidebarTab == 2;

        public bool HasSelectedFiles => SelectedFiles.Count > 0;
        public bool HasNoSelectedFiles => SelectedFiles.Count == 0;
        public int TotalFileCount => SelectedFiles.Count;
        public string FileSelectionSummary => TotalFileCount == 0 ? "파일 미선택" : $"총 {TotalFileCount}개 선택됨";

        public string FileSelectionDetailSummary
        {
            get
            {
                if (TotalFileCount == 0) return "선택된 파일 없음";
                int hwpCount = SelectedFiles.Count(x => x.Extension == ".hwp");
                int hwpxCount = SelectedFiles.Count(x => x.Extension == ".hwpx");
                int pdfCount = SelectedFiles.Count(x => x.Extension == ".pdf");
                int otherCount = TotalFileCount - (hwpCount + hwpxCount + pdfCount);

                var parts = new List<string>();
                if (hwpCount > 0) parts.Add($"HWP {hwpCount}개");
                if (hwpxCount > 0) parts.Add($"HWPX {hwpxCount}개");
                if (pdfCount > 0) parts.Add($"PDF {pdfCount}개");
                if (otherCount > 0) parts.Add($"기타 {otherCount}개");

                return $"총 {TotalFileCount}개 문서 ({string.Join(", ", parts)}) - {TotalSelectedFilesSizeFormatted}";
            }
        }

        public string TotalSelectedFilesSizeFormatted
        {
            get
            {
                long bytes = SelectedFiles.Sum(f => f.FileSizeBytes);
                double kb = bytes / 1024.0;
                if (kb < 1024.0) return $"{kb:F1} KB";
                double mb = kb / 1024.0;
                if (mb < 1024.0) return $"{mb:F2} MB";
                double gb = mb / 1024.0;
                return $"{gb:F2} GB";
            }
        }

        public DiskItem? SelectedDisk
        {
            get => _selectedDisk;
            set
            {
                if (_selectedDisk != value)
                {
                    if (_selectedDisk != null) _selectedDisk.IsSelected = false;

                    if (value != null && value.IsAddCard)
                    {
                        AddImageFile();
                        return; // Do not set SelectedDisk to AddCard
                    }

                    _selectedDisk = value;
                    if (_selectedDisk != null)
                    {
                        _selectedDisk.IsSelected = true;
                    }

                    OnPropertyChanged();
                    (GoToNextStepCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        #endregion

        #region Logic
        private void LoadDisks()
        {
            Disks.Clear();
            DiskDevicesList.Clear();
            ImageFilesList.Clear();
            SelectedDisk = null;

            var driveList = DiskService.GetAvailableDisks();
            foreach (var drive in driveList)
            {
                Disks.Add(drive);
                DiskDevicesList.Add(drive);
            }

            var addCard = new DiskItem
            {
                DiskIndexStr = "+",
                DriveLetter = "+",
                VolumeLabel = "이미지 파일 추가",
                ModelName = "E01, Ex01, dd",
                SerialNumber = "E01, Ex01, dd",
                DriveTypeStr = "E01, Ex01, dd",
                TotalSizeGb = 0,
                FreeSpaceGb = 0,
                IsAddCard = true,
                IsSelected = false
            };

            ImageFilesList.Add(addCard);

            if (DiskDevicesList.Count > 0)
            {
                SelectedDisk = DiskDevicesList.First();
            }
        }

        private void RaiseNextCanExecute() => (GoToNextStepCommand as RelayCommand)?.RaiseCanExecuteChanged();

        private void AddFiles()
        {
            var dlg = new OpenFileDialog
            {
                Title = "문서 파일 선택 (HWP, HWPX, PDF)",
                Filter = "문서 파일(*.hwp;*.hwpx;*.pdf)|*.hwp;*.hwpx;*.pdf|HWP 문서 (*.hwp)|*.hwp|HWPX 문서 (*.hwpx)|*.hwpx|PDF 문서 (*.pdf)|*.pdf|모든 파일 (*.*)|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                bool addedAny = false;
                foreach (string path in dlg.FileNames)
                {
                    if (!SelectedFiles.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    {
                        long size = 0;
                        try
                        {
                            var fi = new FileInfo(path);
                            size = fi.Length;
                        }
                        catch { }

                        SelectedFiles.Add(new SelectedFileItem
                        {
                            FilePath = path,
                            FileSizeBytes = size
                        });
                        addedAny = true;
                    }
                }

                if (addedAny)
                {
                    NotifySelectedFilesChanged();
                }
            }
        }

        private void RemoveFile(SelectedFileItem item)
        {
            if (SelectedFiles.Remove(item))
            {
                NotifySelectedFilesChanged();
            }
        }

        private void ClearAllFiles()
        {
            if (SelectedFiles.Count > 0)
            {
                SelectedFiles.Clear();
                NotifySelectedFilesChanged();
            }
        }

        private void NotifySelectedFilesChanged()
        {
            OnPropertyChanged(nameof(HasSelectedFiles));
            OnPropertyChanged(nameof(HasNoSelectedFiles));
            OnPropertyChanged(nameof(TotalFileCount));
            OnPropertyChanged(nameof(FileSelectionSummary));
            OnPropertyChanged(nameof(FileSelectionDetailSummary));
            OnPropertyChanged(nameof(TotalSelectedFilesSizeFormatted));
            RaiseNextCanExecute();
        }

        private void AddImageFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "이미지 파일 선택 (E01, DD)",
                Filter = "이미지 파일 (*.E01;*.Ex01;*.dd;*.001)|*.E01;*.Ex01;*.dd;*.001|EnCase 이미지 (*.E01;*.Ex01)|*.E01;*.Ex01|DD 이미지 (*.dd;*.001)|*.dd;*.001|모든 파일 (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;
                var inspection = DiskImageService.InspectImageFileSystems(selectedPath);

                if (!inspection.IsValidSupportedImage)
                {
                    MessageBox.Show(
                        $"선택한 이미지 파일 [{Path.GetFileName(selectedPath)}]에서 인식 가능한 파일시스템(NTFS, FAT, exFAT, EXT)을 가진 파티션을 찾을 수 없습니다.\n\n {inspection.ErrorMessage}",
                        "지원되지 않는 파일시스템",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var imageItem = DiskImageService.CreateDiskItemFromImageFile(selectedPath, inspection);
                if (imageItem != null)
                {
                    imageItem.DiskIndexStr = $"이미지 {ImageFilesList.Count}";

                    var existing = ImageFilesList.FirstOrDefault(d => d.IsImageFile && d.ImagePath.Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
                    if (existing != null) ImageFilesList.Remove(existing);

                    int insertIdx = Math.Max(0, ImageFilesList.Count - 1);
                    ImageFilesList.Insert(insertIdx, imageItem);

                    Disks.Add(imageItem);
                    SelectedSidebarTab = 1;
                    SelectedDisk = imageItem;
                }
            }
        }
        #endregion
    }
}
