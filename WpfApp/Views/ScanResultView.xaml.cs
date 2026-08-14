using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfApp.ViewModels;

namespace WpfApp.Views
{
    public partial class ScanResultView : UserControl
    {
        private ScanResultViewModel? _currentViewModel;

        public ScanResultView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_currentViewModel != null)
            {
                _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            if (e.NewValue is ScanResultViewModel vm)
            {
                _currentViewModel = vm;
                _currentViewModel.PropertyChanged += OnViewModelPropertyChanged;
                UpdateWebViewer();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScanResultViewModel.SelectedFile) ||
                e.PropertyName == nameof(ScanResultViewModel.IsPdfPreview) ||
                e.PropertyName == nameof(ScanResultViewModel.PreviewUri) ||
                e.PropertyName == nameof(ScanResultViewModel.PreviewHtmlContent))
            {
                Dispatcher.InvokeAsync(UpdateWebViewer);
            }
        }

        private async void UpdateWebViewer()
        {
            if (_currentViewModel == null) return;

            try
            {
                if (MainWebView.CoreWebView2 == null)
                {
                    // 실행 파일이 위치한 경로(USB) 내 DSAT_Temp\WebView2_Cache 생성 시도
                    string exeDir = AppContext.BaseDirectory;
                    string usbTempDir = Path.Combine(exeDir, "DSAT_Temp");
                    string userDataFolder = Path.Combine(usbTempDir, "WebView2_Cache");

                    try
                    {
                        Directory.CreateDirectory(userDataFolder);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                    {
                        _currentViewModel.SetPreviewError("USB 드라이브에 쓰기 권한이 없습니다. (보안 매체 / USB 쓰기 금지 환경)\n\n실행 파일 경로에 임시 폴더(DSAT_Temp)를 생성할 수 없어 미리보기를 불러올 수 없습니다.");
                        return;
                    }

                    var environment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    await MainWebView.EnsureCoreWebView2Async(environment);

                    if (MainWebView.CoreWebView2?.Settings != null)
                    {
                        MainWebView.CoreWebView2.Settings.IsScriptEnabled = false;
                        MainWebView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                        MainWebView.CoreWebView2.Settings.IsWebMessageEnabled = false;
                        MainWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                    }
                }

                if (_currentViewModel.IsPdfPreview && _currentViewModel.PreviewUri != null)
                {
                    MainWebView.Source = _currentViewModel.PreviewUri;
                }
                else if (!string.IsNullOrEmpty(_currentViewModel.PreviewHtmlContent))
                {
                    MainWebView.NavigateToString(_currentViewModel.PreviewHtmlContent);
                }
                else
                {
                    MainWebView.NavigateToString("<html><body style='font-family:sans-serif;padding:20px;color:#64748B;'>미리보기를 불러올 수 없거나 빈 문서입니다.</body></html>");
                }
            }
            catch (Exception ex)
            {
                _currentViewModel.SetPreviewError($"미리보기 로드 실패 (USB 쓰기 권한 제한 또는 브라우저 오류): {ex.Message}");
            }
        }

        private void FileDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (_currentViewModel == null) return;

            e.Handled = true;
            _currentViewModel.SortByColumn(e.Column.SortMemberPath);
        }
    }
}
