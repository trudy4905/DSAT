using System.Windows.Controls;
using WpfApp.ViewModels;

namespace WpfApp.Views
{
    public partial class ScanResultView : UserControl
    {
        public ScanResultView()
        {
            InitializeComponent();
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
