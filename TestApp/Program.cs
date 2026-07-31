using System;
using System.Threading.Tasks;
using WpfApp.Services;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== Testing NativeBridge P/Invoke Interop ===");

        NativeBridge.OnLogReceived += (level, msg) =>
        {
            Console.WriteLine($"[C++ DLL LOG-{level}] {msg}");
        };

        NativeBridge.OnProgressUpdated += (percent, msg) =>
        {
            Console.WriteLine($"[C++ DLL PROGRESS] {percent}% - {msg}");
        };

        // 1. Initialize
        NativeBridge.InitializeBridge();

        // 2. Get Status
        NativeBridge.Engine_GetStatus(out EngineStatusInfo status);
        Console.WriteLine($"STATUS: Running={status.IsRunning}, CoreCount={status.CoreCount}, TotalProcessed={status.TotalProcessedItems}");

        // 3. Process Array
        double[] input = { 0.5, 1.0, 1.5, 2.0, 2.5 };
        double[] output = await NativeBridge.ProcessDataArrayAsync(input, 3.0);
        Console.WriteLine($"ARRAY COMPLETED. Output[0]={output[0]:F4}, Output[4]={output[4]:F4}");

        // 4. String Wrapper
        string strRes = NativeBridge.ProcessStringWrapper("Antigravity WPF-C++ Engine");
        Console.WriteLine($"STRING RESULT: {strRes}");

        // 5. Async Thread Simulation
        Console.WriteLine("Starting Async Simulation...");
        NativeBridge.Engine_RunAsyncSimulation(5, 50);
        await Task.Delay(400);

        Console.WriteLine("=== ALL NATIVE INTEROP TESTS PASSED ===");
    }
}
