#if MACOS
using System.Runtime.InteropServices;
using Core.Capturing;
using Shouldly;
using Xunit.Abstractions;

namespace Tests.App;

public class LibScreenStreamTests(ITestOutputHelper testOutputHelper)
{
    private ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public async Task When_getting_display_info()
    {
        SemaphoreSlim semaphore = new(0, 1);

        void Callback(IntPtr displayInfos, int count)
        {
            // get NSArray of DisplayInfo from pointer
            var structSize = Marshal.SizeOf<LibScreenStream.DisplayInfo>();
            for (int i = 0; i < count; i++)
            {
                var current = IntPtr.Add(displayInfos, i * structSize);
                var display = Marshal.PtrToStructure<LibScreenStream.DisplayInfo>(current);

                TestOutputHelper.WriteLine(
                    $"Display {display.Id}: ({display.Width}x{display.Height})");
            }

            semaphore.Release();
        }

        var result = LibScreenStream.GetAvailableDisplays(Callback);
        result.ShouldBe(0);

        await semaphore.WaitAsync();
    }
}
#endif