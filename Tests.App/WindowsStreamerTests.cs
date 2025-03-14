using Core.Capturing;
using Core.Image;
using Moq;

namespace Tests.App;

public class WindowsStreamerTests
{
    private EventSourceMock EventSourceMock { get; } = new();

    [Fact]
    public void Foo()
    {
        var sut = new WindowsStreamer(EventSourceMock);
    }
}