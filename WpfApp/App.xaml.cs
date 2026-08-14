using System;
using System.IO;
using System.Windows;
using WpfApp.Services;

namespace WpfApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// 현장 포렌식 무흔적(Zero-Trace) 원칙에 따라 앱 종료/비상종료 시 잔여 임시 디렉토리를 자동 완전 수거합니다.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.ProcessExit += (s, ev) => CleanupTemporaryFiles();
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => CleanupTemporaryFiles();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CleanupTemporaryFiles();
            base.OnExit(e);
        }

        private static void CleanupTemporaryFiles()
        {
            try
            {
                // 1. USB 실행 드라이브 내 DSAT_Temp 완전 삭제
                string execRoot = DiskService.GetExecutionDriveRoot();
                if (!string.IsNullOrEmpty(execRoot))
                {
                    string usbTempDir = Path.Combine(execRoot, "DSAT_Temp");
                    if (Directory.Exists(usbTempDir))
                    {
                        Directory.Delete(usbTempDir, recursive: true);
                    }
                }

                // 2. PC %TEMP% 내 혹시 남아있을 DSAT_* 임시 디렉토리 전량 삭제
                string sysTemp = Path.GetTempPath();
                if (Directory.Exists(sysTemp))
                {
                    foreach (var dir in Directory.GetDirectories(sysTemp, "DSAT_*"))
                    {
                        try { Directory.Delete(dir, recursive: true); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
