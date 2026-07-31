using System;
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
        private int _selectedSidebarTab = 0; // 0 = 디스크 장치, 1 = 이미지 파일
        private DiskItem? _selectedDisk;

        public ObservableCollection<DiskItem> Disks { get; } = new ObservableCollection<DiskItem>();
        public ObservableCollection<DiskItem> DiskDevicesList { get; } = new ObservableCollection<DiskItem>();
        public ObservableCollection<DiskItem> ImageFilesList { get; } = new ObservableCollection<DiskItem>();

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

            AddImageCardCommand = new RelayCommand(_ => AddImageFile());

            RefreshDisksCommand = new RelayCommand(_ => LoadDisks());

            GoToNextStepCommand = new RelayCommand(_ =>
            {
                if (SelectedDisk != null && !SelectedDisk.IsAddCard)
                {
                    ScanRequested?.Invoke(this, SelectedDisk);
                }
            }, _ => SelectedDisk != null && !SelectedDisk.IsAddCard);

            LoadDisks();
        }

        #region Commands
        public ICommand SelectSidebarTabCommand { get; }
        public ICommand SelectDiskCardCommand { get; }
        public ICommand AddImageCardCommand { get; }
        public ICommand RefreshDisksCommand { get; }
        public ICommand GoToNextStepCommand { get; }
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
                    if (_selectedDisk != null) _selectedDisk.IsSelected = true;

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

        private void AddImageFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "EnCase 포렌식 또는 RAW 디스크 이미지 선택",
                Filter = "Disk Image Files (*.E01;*.Ex01;*.raw;*.dd;*.img;*.iso;*.vhd;*.vmdk)|*.E01;*.Ex01;*.raw;*.dd;*.img;*.iso;*.vhd;*.vmdk|EnCase Image (*.E01;*.Ex01)|*.E01;*.Ex01|RAW Disk Image (*.raw;*.dd;*.img)|*.raw;*.dd;*.img|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;
                var inspection = DiskImageService.InspectImageFileSystems(selectedPath);

                if (!inspection.IsValidSupportedImage)
                {
                    MessageBox.Show(
                        $"선택한 이미지 파일 [{Path.GetFileName(selectedPath)}]에서 인식 가능한 파일시스템(NTFS, FAT, exFAT, EXT)을 가진 파티션을 찾을 수 없습니다.\n\n[원인] {inspection.ErrorMessage}",
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
