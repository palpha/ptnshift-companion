using Core.Image;
using Moq;

namespace Tests.App;

public class WinStreamerTests
{
    private EventSourceMock EventSourceMock { get; } = new();

    [Fact]
    public void Foo()
    {
        var sut = new WinStreamer(EventSourceMock);

        
    }
}