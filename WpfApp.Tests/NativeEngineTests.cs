using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WpfApp.Services;
using WpfApp.Models;
using Xunit;

namespace WpfApp.Tests
{
    public class NativeEngineTests
    {
        [Fact]
        public void TestEngineInitializationAndStatus()
        {
            NativeBridge.InitializeBridge();
            
            NativeBridge.Engine_GetStatus(out EngineStatusInfo status);

            Assert.Equal(1, status.IsRunning);
            Assert.True(status.CoreCount > 0, "Core count should be positive.");
        }

        [Fact]
        public void TestProcessDataArray()
        {
            NativeBridge.InitializeBridge();

            double[] input = new double[] { 1.0, 1.57, 3.14, 4.71 };
            double[] output = new double[input.Length];
            double multiplier = 2.0;

            int result = NativeBridge.Engine_ProcessDataArray(input, output, input.Length, multiplier);

            Assert.Equal(1, result);
            Assert.Equal(input.Length, output.Length);
            Assert.NotEqual(0.0, output[0]);
        }

        [Fact]
        public void TestStringMarshalling()
        {
            NativeBridge.InitializeBridge();

            string testStr = "Test C++ Interop String";
            string result = NativeBridge.ProcessStringWrapper(testStr);

            Assert.Contains("Test C++ Interop String", result);
            Assert.StartsWith("[C++ Engine OOP Facade]:", result);
        }

        [Fact]
        public async Task TestDocumentPreviewServiceVirtualImageText()
        {
            var resPdf = await DocumentPreviewService.ExtractTextAsync("[DiskImage.E01: Partition 1]/Documents/sample.pdf");
            Assert.True(resPdf.Success);
            Assert.Contains("PDF", resPdf.FormatType);

            var resHwp = await DocumentPreviewService.ExtractTextAsync("[DiskImage.E01: Partition 1]/Documents/sample.hwp");
            Assert.True(resHwp.Success);
            Assert.Contains("HWP", resHwp.FormatType);
        }

        [Fact]
        public async Task TestDocumentPreviewServiceRealPdfFile()
        {
            string tempPdf = Path.Combine(Path.GetTempPath(), "test_sample.pdf");
            string pdfContent = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n3 0 obj\n<< /Type /Page /Parent 2 0 R /Contents 4 0 R >>\nendobj\n4 0 obj\n<< /Length 55 >>\nstream\nBT\n/F1 12 Tf\n70 50 Td\n(Hello World PDF Preview Test) Tj\nET\nendstream\nendobj\nxref\n0 5\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n0000000174 00000 n \ntrailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n279\n%%EOF";
            File.WriteAllText(tempPdf, pdfContent, Encoding.ASCII);

            try
            {
                var res = await DocumentPreviewService.ExtractTextAsync(tempPdf);
                Assert.True(res.Success);
                Assert.Contains("Hello World PDF Preview Test", res.ContentText);
            }
            finally
            {
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
            }
        }

        [Fact]
        public void TestKoreanFolderPathHandling()
        {
            NativeBridge.InitializeBridge();

            string koreanDir = Path.Combine(Path.GetTempPath(), "포렌식_한글_폴더_테스트");
            Directory.CreateDirectory(koreanDir);
            string koreanPdfPath = Path.Combine(koreanDir, "샘플_문서.pdf");

            string pdfContent = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n3 0 obj\n<< /Type /Page /Parent 2 0 R /Contents 4 0 R >>\nendobj\n4 0 obj\n<< /Length 20 >>\nstream\nBT /F1 12 Tf ET\nendstream\nendobj\nxref\n0 5\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n0000000174 00000 n \ntrailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n244\n%%EOF";
            File.WriteAllText(koreanPdfPath, pdfContent, Encoding.ASCII);

            try
            {
                int resOverlay = NativeBridge.Engine_AnalyzeDocumentOverlay(koreanPdfPath, out var overlayResult);
                Assert.Equal(1, resOverlay);
                Assert.Equal(1, overlayResult.IsNormal);

                int resInspect = NativeBridge.Engine_InspectForensicImage(koreanPdfPath, out var inspectResult);
                Assert.Equal(0, resInspect);
                Assert.True(inspectResult.IsValid);

                var diskItem = DiskImageService.CreateDiskItemFromImageFile(koreanPdfPath, new ImageInspectionResult
                {
                    IsValidSupportedImage = true,
                    ImageTypeTag = inspectResult.ImageTypeTag,
                    TotalImageSize = inspectResult.TotalImageSize,
                    TotalPartitionSize = inspectResult.TotalPartitionSize
                });

                Assert.NotNull(diskItem);
                Assert.True(diskItem!.ImageFileSizeBytes > 0);
            }
            finally
            {
                if (File.Exists(koreanPdfPath)) File.Delete(koreanPdfPath);
                if (Directory.Exists(koreanDir)) Directory.Delete(koreanDir);
            }
        }

        [Fact]
        public void TestSplitDdImageReading()
        {
            NativeBridge.InitializeBridge();

            string tempDir = Path.Combine(Path.GetTempPath(), "SplitDdTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            string seg1 = Path.Combine(tempDir, "sample_raw.001");
            string seg2 = Path.Combine(tempDir, "sample_raw.002");

            byte[] buf1 = new byte[512]; Array.Fill<byte>(buf1, (byte)'A');
            byte[] buf2 = new byte[512]; Array.Fill<byte>(buf2, (byte)'B');

            File.WriteAllBytes(seg1, buf1);
            File.WriteAllBytes(seg2, buf2);

            try
            {
                ulong physicalSetSize = DiskImageService.GetPhysicalImageFileSetSize(seg1);
                Assert.Equal(1024UL, physicalSetSize);

                int resInspect = NativeBridge.Engine_InspectForensicImage(seg1, out var inspectResult);
                Assert.Equal(0, resInspect);
                Assert.True(inspectResult.IsValid);
                Assert.Contains("DD/RAW", inspectResult.ImageTypeTag);
                Assert.Equal(1024UL, inspectResult.TotalImageSize);
            }
            finally
            {
                if (File.Exists(seg1)) File.Delete(seg1);
                if (File.Exists(seg2)) File.Delete(seg2);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
            }
        }
    }
}
