using System.Windows;
using System.Windows.Input;

namespace WpfApp.Views
{
    public partial class NoResultDialog : Window
    {
        public NoResultDialog(string fileName, string fsInfo)
        {
            InitializeComponent();

            TitleText.Text = "탐색된 파일 없음";
            SubText.Text = "이미지 파일에서 HWP / HWPX 문서를\n찾을 수 없습니다.";
            FileNameText.Text = fileName;
            FsInfoText.Text = string.IsNullOrWhiteSpace(fsInfo) ? string.Empty : $"· {fsInfo}";
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e) => Close();
        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}
