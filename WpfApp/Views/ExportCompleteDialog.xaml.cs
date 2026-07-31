using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace WpfApp.Views
{
    public partial class ExportCompleteDialog : Window
    {
        public string TargetFolderPath { get; }

        public ExportCompleteDialog(int exportedCount, string targetFolderPath)
        {
            InitializeComponent();
            TargetFolderPath = targetFolderPath;

            if (exportedCount == 0 && string.IsNullOrEmpty(targetFolderPath))
            {
                TxtSummary.Text = "선택된 파일이 없습니다.";
                TxtFolderPath.Text = "리스트에서 체크박스를 선택해 주세요.";
                BtnOpenFolder.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtSummary.Text = $"총 {exportedCount:N0}개 파일 내보내기 성공";
                TxtFolderPath.Text = targetFolderPath;
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(TargetFolderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{TargetFolderPath}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }

            DialogResult = true;
            Close();
        }
    }
}
