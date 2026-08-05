using System;
using System.Windows;
using WpfApp.ViewModels;

namespace WpfApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // 메인 윈도우 종료 시 백그라운드 스레드 및 WebView2/C++ DLL 핸들을 완전히 해제하고 깨끗하게 종료
            Environment.Exit(0);
        }
    }
}