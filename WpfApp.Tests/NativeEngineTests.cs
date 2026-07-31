using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WpfApp.Services;
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
    }
}
