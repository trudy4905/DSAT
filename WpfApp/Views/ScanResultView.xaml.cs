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
                else if (!string.IsNullOrEmpty(_currentViewModel.PreviewText))
                {
                    string fallbackHtml = HwpHtmlDocumentGenerator.GenerateHwpHtmlDocument(
                        _currentViewModel.PreviewTitle,
                        _currentViewModel.PreviewText,
                        _currentViewModel.PreviewFormatType,
                        _currentViewModel.PreviewFilePath,
                        _currentViewModel.PreviewFileSize,
                        _currentViewModel.PreviewLastModified
                    );
                    MainWebView.NavigateToString(fallbackHtml);
                }
            }
            catch { }
        }

        private void FileDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (e.Column.SortMemberPath == "Index")
            {
                e.Handled = true; // Disable sorting for '번호' column
                return;
            }

            if (DataContext is ScanResultViewModel vm && e.Column.SortMemberPath != null)
            {
                if (vm.SortColumnCommand.CanExecute(e.Column.SortMemberPath))
                {
                    vm.SortColumnCommand.Execute(e.Column.SortMemberPath);
                }

                // Update headers (workaround since Column Headers don't inherit DataContext perfectly)
                foreach (var col in FileDataGrid.Columns)
                {
                    if (col.SortMemberPath == "Index") col.Header = vm.HeaderIndex;
                    else if (col.SortMemberPath == "StatusText") col.Header = vm.HeaderStatus;
                    else if (col.SortMemberPath == "FileName") col.Header = vm.HeaderFileName;
                    else if (col.SortMemberPath == "FileSizeBytes") col.Header = vm.HeaderFileSize;
                    else if (col.SortMemberPath == "Extension") col.Header = vm.HeaderExtension;
                    else if (col.SortMemberPath == "FilePath") col.Header = vm.HeaderFilePath;
                    else if (col.SortMemberPath == "CreatedTime") col.Header = vm.HeaderCreatedTime;
                    else if (col.SortMemberPath == "LastModified") col.Header = vm.HeaderLastModified;
                    else if (col.SortMemberPath == "TextSnippet") col.Header = vm.HeaderTextSnippet;
                }

                e.Handled = true;
            }
        }
    }
}
