using System;
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

            double[] input = new double[] { 0.0, 1.57, 3.14, 4.71 };
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
            Assert.StartsWith("[C++ Engine Processed]", result);
        }
    }
}
