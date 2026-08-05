using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WpfApp.Services;
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
                await MainWebView.EnsureCoreWebView2Async();

                // Security Lockdown: Disable scripts, host objects, and dialogs
                MainWebView.CoreWebView2.Settings.IsScriptEnabled = false;
                MainWebView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                MainWebView.CoreWebView2.Settings.IsWebMessageEnabled = false;
                MainWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

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
                System.Diagnostics.Debug.WriteLine($"WebView2 Error: {ex.Message}");
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
