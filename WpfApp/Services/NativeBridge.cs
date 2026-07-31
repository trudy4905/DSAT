using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp.Services
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct EngineStatusInfo
    {
        public int IsRunning;
        public int CoreCount;
        public double LastExecutionTimeMs;
        public ulong TotalProcessedItems;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct DocumentAnalysisResult
    {
        public int IsNormal;
        public int HasOverlay;
        public int RiskLevel;
        public int FindingCount;
        public ulong LogicalSize;
        public ulong PhysicalSize;
        public ulong OverlaySize;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string DetectedFormat;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] StatusMessageBytes;

        public string StatusMessage
        {
            get
            {
                if (StatusMessageBytes == null || StatusMessageBytes.Length == 0) return string.Empty;
                int nullIdx = Array.IndexOf(StatusMessageBytes, (byte)0);
                int len = nullIdx >= 0 ? nullIdx : StatusMessageBytes.Length;
                return System.Text.Encoding.UTF8.GetString(StatusMessageBytes, 0, len);
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ProgressCallbackDelegate(int progressPercent, IntPtr statusMessage);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogCallbackDelegate(int logLevel, IntPtr logMessage);

    public static class NativeBridge
    {
        private const string DllName = "NativeEngine.dll";

        private static ProgressCallbackDelegate? _progressDelegateHolder;
        private static LogCallbackDelegate? _logDelegateHolder;

        public static event Action<int, string>? OnProgressUpdated;
        public static event Action<int, string>? OnLogReceived;

        static NativeBridge()
        {
            try
            {
                NativeLibrary.SetDllImportResolver(typeof(NativeBridge).Assembly, (libraryName, assembly, searchPath) =>
                {
                    if (libraryName == DllName)
                    {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string path1 = Path.Combine(baseDir, DllName);
                        if (File.Exists(path1)) return NativeLibrary.Load(path1);

                        string asmDir = Path.GetDirectoryName(assembly.Location) ?? string.Empty;
                        string path2 = Path.Combine(asmDir, DllName);
                        if (!string.IsNullOrEmpty(asmDir) && File.Exists(path2)) return NativeLibrary.Load(path2);

                        string[] candidatePaths = new[]
                        {
                            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\NativeEngine\NativeEngine.dll")),
                            Path.GetFullPath(Path.Combine(asmDir, @"..\..\..\..\NativeEngine\NativeEngine.dll")),
                            @"C:\Users\user\.gemini\antigravity-ide\scratch\WpfCppEngineApp\NativeEngine\NativeEngine.dll"
                        };

                        foreach (var path in candidatePaths)
                        {
                            if (File.Exists(path))
                            {
                                return NativeLibrary.Load(path);
                            }
                        }
                    }
                    return IntPtr.Zero;
                });
            }
            catch (InvalidOperationException)
            {
                // Ignored
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_Initialize();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_Shutdown();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Engine_GetStatus(out EngineStatusInfo status);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Engine_SetProgressCallback(ProgressCallbackDelegate callback);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Engine_SetLogCallback(LogCallbackDelegate callback);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_ProcessDataArray(
            [In] double[] inputData,
            [Out] double[] outputData,
            int dataLength,
            double multiplier);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Engine_RunAsyncSimulation(int totalSteps, int stepDelayMs);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_ProcessString(
            [MarshalAs(UnmanagedType.LPStr)] string inputStr,
            [Out] StringBuilder outputBuffer,
            int bufferSize);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Engine_AnalyzeDocumentOverlay(
            [MarshalAs(UnmanagedType.LPStr)] string filePath,
            out DocumentAnalysisResult outResult);

        public static void InitializeBridge()
        {
            Engine_Initialize();

            _progressDelegateHolder = HandleProgress;
            _logDelegateHolder = HandleLog;

            Engine_SetProgressCallback(_progressDelegateHolder);
            Engine_SetLogCallback(_logDelegateHolder);
        }

        private static void HandleProgress(int progressPercent, IntPtr statusMessagePtr)
        {
            try
            {
                string statusMessage = Marshal.PtrToStringAnsi(statusMessagePtr) ?? string.Empty;
                OnProgressUpdated?.Invoke(progressPercent, statusMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Progress Callback: {ex.Message}");
            }
        }

        private static void HandleLog(int logLevel, IntPtr logMessagePtr)
        {
            try
            {
                string logMessage = Marshal.PtrToStringAnsi(logMessagePtr) ?? string.Empty;
                OnLogReceived?.Invoke(logLevel, logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Log Callback: {ex.Message}");
            }
        }

        public static Task<double[]> ProcessDataArrayAsync(double[] inputData, double multiplier)
        {
            return Task.Run(() =>
            {
                double[] outputData = new double[inputData.Length];
                int result = Engine_ProcessDataArray(inputData, outputData, inputData.Length, multiplier);
                if (result == 0)
                {
                    throw new InvalidOperationException("Failed to process data array in C++ Engine.");
                }
                return outputData;
            });
        }

        public static string ProcessStringWrapper(string input)
        {
            StringBuilder sb = new StringBuilder(1024);
            int res = Engine_ProcessString(input, sb, sb.Capacity);
            return res != 0 ? sb.ToString() : "Error processing string";
        }
    }
}
