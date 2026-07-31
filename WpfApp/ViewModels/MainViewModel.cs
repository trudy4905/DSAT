using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WpfApp.Models;
using WpfApp.Services;

namespace WpfApp.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private ObservableObject _currentViewModel;
        private readonly SelectionViewModel _selectionViewModel;
        private readonly ScanResultViewModel _scanResultViewModel;
        
        private bool _isScanning;
        private bool _isScanOverlayVisible;
        private double _scanProgressPercentage;
        private string _scanStatusMessage = "대기 중...";
        private string _scanProgressText = "0개 항목 발견됨";
        
        private bool _isEngineInitialized;
        private int _coreCount;
        private CancellationTokenSource? _scanCts;
        private readonly DocumentFileScannerService _scannerService = new DocumentFileScannerService();

        public MainViewModel()
        {
            _selectionViewModel = new SelectionViewModel();
            _selectionViewModel.ScanRequested += OnScanRequested;

            _scanResultViewModel = new ScanResultViewModel();
            _scanResultViewModel.RequestGoBack += OnRequestGoBack;
            _scanResultViewModel.RequestRefreshScan += OnRequestRefreshScan;

            // Start at Selection View
            _currentViewModel = _selectionViewModel;

            CancelScanCommand = new RelayCommand(_ => CancelScan());
            CloseOverlayCommand = new RelayCommand(_ => { IsScanOverlayVisible = false; });

            InitializeNativeEngine();
        }

        #region ViewModel Routing
        public ObservableObject CurrentViewModel
        {
            get => _currentViewModel;
            set { SetProperty(ref _currentViewModel, value); }
        }

        private void OnScanRequested(object? sender, DiskItem disk)
        {
            _ = StartDiskScanAsync(disk);
        }

        private void OnRequestGoBack(object? sender, EventArgs e)
        {
            CancelScan();
            CurrentViewModel = _selectionViewModel;
        }

        private void OnRequestRefreshScan(object? sender, EventArgs e)
        {
            if (_selectionViewModel.SelectedDisk != null)
            {
                _ = StartDiskScanAsync(_selectionViewModel.SelectedDisk);
            }
        }
        #endregion

        #region Overlay & Scan Progress Properties
        public bool IsScanOverlayVisible
        {
            get => _isScanOverlayVisible;
            set { SetProperty(ref _isScanOverlayVisible, value); }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set { SetProperty(ref _isScanning, value); }
        }

        public double ScanProgressPercentage
        {
            get => _scanProgressPercentage;
            set { SetProperty(ref _scanProgressPercentage, value); }
        }

        public string ScanStatusMessage
        {
            get => _scanStatusMessage;
            set { SetProperty(ref _scanStatusMessage, value); }
        }

        public string ScanProgressText
        {
            get => _scanProgressText;
            set { SetProperty(ref _scanProgressText, value); }
        }

        public ICommand CancelScanCommand { get; }
        public ICommand CloseOverlayCommand { get; }
        #endregion

        #region Scanning Logic
        private void CancelScan()
        {
            if (_scanCts != null && !_scanCts.IsCancellationRequested)
            {
                _scanCts.Cancel();
                _scanCts.Dispose();
                _scanCts = null;
            }
            IsScanning = false;
            IsScanOverlayVisible = false;
        }

        private async Task StartDiskScanAsync(DiskItem targetDisk)
        {
            CancelScan();

            _scanCts = new CancellationTokenSource();
            
            // Prepare Result VM State
            _scanResultViewModel.SelectedDisk = targetDisk;
            _scanResultViewModel.FileList.Clear();
            _scanResultViewModel.FilteredFileList.Clear();
            _scanResultViewModel.SelectedFile = null;

            IsScanning = true;
            IsScanOverlayVisible = true;
            ScanProgressPercentage = 0;
            ScanStatusMessage = $"{targetDisk.VolumeLabel} 문서 탐색 중...";
            ScanProgressText = "0개 파일 발견됨";

            // Dispatcher UI render yield so the Overlay Modal appears immediately
            await Task.Delay(150);

            var progress = new Progress<(int scannedDirs, string currentFolder, HwpFileItem? foundFile)>(data =>
            {
                ScanStatusMessage = $"검색 중: {data.currentFolder}";

                if (data.foundFile != null)
                {
                    _scanResultViewModel.FileList.Add(data.foundFile);
                    ScanProgressText = $"{_scanResultViewModel.FileList.Count:N0}개 파일 발견";
                }
            });

            bool keepOverlayOpen = false;
            try
            {
                // Run background scan on a worker thread so UI thread stays 100% responsive
                var results = await Task.Run(() => _scannerService.ScanTargetAsync(targetDisk, progress, _scanCts.Token));

                if (results.Count == 0)
                {
                    ScanProgressPercentage = 100;
                    ScanStatusMessage = "검색 결과 없음";
                    ScanProgressText = "선택한 대상에서 HWP/HWPX/PDF 문서를 찾지 못했습니다.";
                    IsScanning = false;
                    _scanResultViewModel.HasNoScanResult = true;
                    keepOverlayOpen = true; // Keep modal open for user feedback
                    return;
                }

                int hwpCount = results.Count(x => x.Extension.Equals(".hwp", StringComparison.OrdinalIgnoreCase));
                int hwpxCount = results.Count(x => x.Extension.Equals(".hwpx", StringComparison.OrdinalIgnoreCase));
                int pdfCount = results.Count(x => x.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase));

                ScanStatusMessage = $"탐색 완료 ({targetDisk.VolumeLabel})";
                ScanProgressText = $"총 {results.Count:N0}개 문서 (HWP: {hwpCount:N0}개, HWPX: {hwpxCount:N0}개, PDF: {pdfCount:N0}개)";
                
                _scanResultViewModel.HasNoScanResult = false;
                _scanResultViewModel.ScanProgressText = ScanProgressText;
                _scanResultViewModel.InitializeResults(results);

                await Task.Delay(300); // Visual feedback pause

                CurrentViewModel = _scanResultViewModel;
            }
            catch (OperationCanceledException)
            {
                ScanStatusMessage = "검색이 취소되었습니다.";
            }
            catch (Exception ex)
            {
                ScanStatusMessage = $"검색 중 오류 발생: {ex.Message}";
                keepOverlayOpen = true;
            }
            finally
            {
                IsScanning = false;
                if (!keepOverlayOpen)
                {
                    IsScanOverlayVisible = false;
                }
            }
        }
        #endregion

        #region Native Engine Initialization
        private void InitializeNativeEngine()
        {
            try
            {
                NativeBridge.InitializeBridge();
                NativeBridge.Engine_GetStatus(out var status);
                _isEngineInitialized = status.IsRunning == 1;
                _coreCount = status.CoreCount;

                if (_isEngineInitialized)
                {
                    Console.WriteLine($"[C++ Engine] 엔진이 성공적으로 초기화되었습니다. (활성 코어: {_coreCount}개)");
                }
                else
                {
                    Console.WriteLine($"[C++ Engine] 초기화 실패: Engine is not running.");
                    MessageBox.Show($"C++ 분석 엔진 초기화에 실패했습니다.\n일부 기능이 제한될 수 있습니다.",
                                    "엔진 초기화 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C++ Engine] 예외 발생: {ex.Message}");
            }
        }
        #endregion
    }
}
