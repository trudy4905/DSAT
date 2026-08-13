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

    public enum DetectionRuleType
    {
        None = 0,
        Overlay = 1,
        MacroScript = 2,
        EncryptedStream = 3,
        OleSlack = 4
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct NativeFindingItem
    {
        public int RiskLevel;
        public int RuleType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] TitleBytes;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] DescriptionBytes;

        public string Title => GetUtf8String(TitleBytes);
        public string Description => GetUtf8String(DescriptionBytes);

        private static string GetUtf8String(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            int nullIdx = Array.IndexOf(bytes, (byte)0);
            int len = nullIdx >= 0 ? nullIdx : bytes.Length;
            return System.Text.Encoding.UTF8.GetString(bytes, 0, len);
        }
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

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public NativeFindingItem[] Findings;

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

    #region Forensic Image Inspection Structures & P/Invoke
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct PartitionItemInfo
    {
        public int PartitionIndex;
        public int SectorSize;
        public ulong StartSector;
        public ulong SectorCount;
        public fixed byte FilesystemBytes[32];
        [MarshalAs(UnmanagedType.U1)]
        public bool IsSupported;

        public string Filesystem
        {
            get
            {
                fixed (byte* p = FilesystemBytes)
                {
                    int len = 0;
                    while (len < 32 && p[len] != 0) len++;
                    return Encoding.UTF8.GetString(p, len);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public unsafe struct ImageInspectionOutput
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool IsValid;
        public fixed byte ImageTypeTagBytes[64];
        public ulong TotalImageSize;
        public ulong TotalPartitionSize;
        public int PartitionCount;
        public PartitionItemInfo p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15;
        public fixed byte ErrorMessageBytes[256];

        public PartitionItemInfo[] Partitions
        {
            get
            {
                fixed (PartitionItemInfo* p = &p0)
                {
                    int count = PartitionCount > 16 ? 16 : (PartitionCount < 0 ? 0 : PartitionCount);
                    PartitionItemInfo[] arr = new PartitionItemInfo[count];
                    for (int i = 0; i < count; i++) arr[i] = p[i];
                    return arr;
                }
            }
        }

        public string ImageTypeTag
        {
            get
            {
                fixed (byte* p = ImageTypeTagBytes)
                {
                    int len = 0;
                    while (len < 64 && p[len] != 0) len++;
                    return Encoding.UTF8.GetString(p, len);
                }
            }
        }

        public string ErrorMessage
        {
            get
            {
                fixed (byte* p = ErrorMessageBytes)
                {
                    int len = 0;
                    while (len < 256 && p[len] != 0) len++;
                    return Encoding.UTF8.GetString(p, len);
                }
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public delegate void ImageScanProgressCallbackDelegate(int scannedCount, string currentPath, string statusMsg);
    #endregion

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void ProgressCallbackDelegate(int progressPercent, IntPtr statusMessage);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void LogCallbackDelegate(int logLevel, IntPtr logMessage);

    public static class NativeBridge
    {
        private const string DllName = "NativeEngine.dll";

        private static ProgressCallbackDelegate? _progressDelegateHolder;
        private static LogCallbackDelegate? _logDelegateHolder;

        public static event Action<int, string>? OnProgressUpdated;
        public static event Action<int, string>? OnLogReceived;
        private static readonly object _logFileLock = new object();
        private static readonly string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EngineLog.txt");

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
                            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\NativeEngine\bin\NativeEngine.dll")),
                            Path.GetFullPath(Path.Combine(asmDir, @"..\..\..\..\NativeEngine\bin\NativeEngine.dll")),
                            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\NativeEngine\NativeEngine.dll")),
                            @"C:\Users\user\.gemini\antigravity-ide\scratch\WpfCppEngineApp\NativeEngine\bin\NativeEngine.dll"
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

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int Engine_Initialize();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void Engine_Shutdown();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void Engine_GetStatus(out EngineStatusInfo status);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern void Engine_SetProgressCallback(ProgressCallbackDelegate callback);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern void Engine_SetLogCallback(LogCallbackDelegate callback);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int Engine_ProcessDataArray(
            [In] double[] inputData,
            [Out] double[] outputData,
            int dataLength,
            double multiplier);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int Engine_AnalyzeDocumentOverlay(
            string filePath,
            out DocumentAnalysisResult outResult);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int Engine_InspectForensicImage(
            string imagePath,
            out ImageInspectionOutput outResult);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int Engine_ExtractDocumentFilesFromImage(
            string imagePath,
            string tempExtractDir,
            int includeDeleted,
            ImageScanProgressCallbackDelegate callback,
            out int outExtractedCount);

        public static string ProcessStringWrapper(string input)
        {
            return $"[C++ Engine OOP Facade]: {input}";
        }

        public static void InitializeBridge()
        {
            RegisterCallbacks();
            try
            {
                Engine_Initialize();
            }
            catch { }
        }

        public static void RegisterCallbacks()
        {
            _progressDelegateHolder = (progress, statusPtr) =>
            {
                string statusMsg = Marshal.PtrToStringAnsi(statusPtr) ?? string.Empty;
                OnProgressUpdated?.Invoke(progress, statusMsg);
            };

            _logDelegateHolder = (logLevel, logPtr) =>
            {
                string logMsg = Marshal.PtrToStringAnsi(logPtr) ?? string.Empty;
                Console.WriteLine($"[C++ NativeEngine] {logMsg}");
                System.Diagnostics.Debug.WriteLine($"[C++ NativeEngine] {logMsg}");
                OnLogReceived?.Invoke(logLevel, logMsg);
                // Write log to file in executable directory
                try
                {
                    lock (_logFileLock)
                    {
                        File.AppendAllText(_logFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Level {logLevel}] {logMsg}{Environment.NewLine}");
                    }
                }
                catch
                {
                    // Ignore any I/O errors to avoid breaking callback flow
                }
            };

            Engine_SetProgressCallback(_progressDelegateHolder);
            Engine_SetLogCallback(_logDelegateHolder);
        }
    }
}
