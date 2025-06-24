using System.Diagnostics.CodeAnalysis;
using Core.Capturing;
using Core.Diagnostics;
using Core.Image;
using Core.Usb;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.Language.Flow;
using Shouldly;

namespace Tests.GUI;

[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
[SuppressMessage("ReSharper", "LocalVariableHidesMember")]
public class Push2UsbTests
{
    private static byte Interface => 0;
    private static int InitResult => 0;
    private static IntPtr UsbContext => 234;
    private static IntPtr PushDevice => 123;

    private int ClaimResult { get; set; } = 0;
    private int ReleaseResult { get; set; } = 0;

    private TestLogger<Push2Usb> Logger { get; } = new();
    private Mock<IStreamer> StreamerMock { get; } = new(MockBehavior.Strict);
    private EventSourceMock EventSourceMock { get; } = new();
    private Mock<ILibUsbWrapper> LibUsbWrapperMock { get; } = new(MockBehavior.Strict);
    private Mock<IDiagnosticOutputRenderer> DiagnosticOutputRendererMock { get; } = new(MockBehavior.Strict);

    private IReturnsResult<ILibUsbWrapper> InitSetup { get; }
    private IReturnsResult<ILibUsbWrapper> OpenDeviceWithVidPidSetup { get; }
    private IReturnsResult<ILibUsbWrapper> ClaimInterfaceSetup { get; }
    private IReturnsResult<ILibUsbWrapper> LibUsbErrorNameSetup { get; }
    private IReturnsResult<ILibUsbWrapper> ReleaseInterfaceSetup { get; }
    private ISetup<ILibUsbWrapper> CloseSetup { get; }
    private ISetup<ILibUsbWrapper> ExitSetup { get; }

    private Push2Usb Sut { get; }

    public Push2UsbTests()
    {
        InitSetup =
            LibUsbWrapperMock
                .Setup(x => x.Init())
                .Returns(() => (InitResult, UsbContext));
        OpenDeviceWithVidPidSetup =
            LibUsbWrapperMock
                .Setup(x => x.OpenDeviceWithVidPid(
                    UsbContext,
                    PushIdentity.Push2.VendorId,
                    PushIdentity.Push2.ProductId))
                .Returns(() => PushDevice);
        ClaimInterfaceSetup =
            LibUsbWrapperMock
                .Setup(x => x.ClaimInterface(PushDevice, Interface))
                .Returns(() => ClaimResult);
        LibUsbErrorNameSetup =
            LibUsbWrapperMock
                .Setup(x => x.LibUsbErrorName(ClaimResult))
                .Returns(() => IntPtr.Zero);
        ReleaseInterfaceSetup =
            LibUsbWrapperMock
                .Setup(x => x.ReleaseInterface(PushDevice, Interface))
                .Returns(() => ReleaseResult);
        CloseSetup = LibUsbWrapperMock.Setup(x => x.Close(PushDevice));
        ExitSetup = LibUsbWrapperMock.Setup(x => x.Exit(UsbContext));
        StreamerMock
            .Setup(x => x.EventSource)
            .Returns(() => EventSourceMock);
        DiagnosticOutputRendererMock.Setup(
            x => x.SetText(It.IsAny<Subsystem>(), It.IsAny<string>(), It.IsAny<bool?>()));

        // The most common situation
        InitSetup.Verifiable(Times.Once);
        OpenDeviceWithVidPidSetup.Verifiable(Times.Once);
        ClaimInterfaceSetup.Verifiable(Times.Once);
        LibUsbErrorNameSetup.Verifiable(Times.Never);
        ReleaseInterfaceSetup.Verifiable(Times.Never);
        CloseSetup.Verifiable(Times.Never);
        ExitSetup.Verifiable(Times.Never);

        Sut = new(
            Logger,
            StreamerMock.Object,
            new ImageConverter(),
            LibUsbWrapperMock.Object,
            new FakeTimeProvider(),
            DiagnosticOutputRendererMock.Object);
    }

    [Fact]
    public void When_connecting()
    {
        DiagnosticOutputRendererMock.Reset();
        DiagnosticOutputRendererMock
            .Setup(x => x.SetText(Subsystem.FrameTransmission, "Connected", true))
            .Verifiable(Times.Once);

        var result = Sut.Connect();

        result.ShouldBeTrue();
        Sut.IsConnected.ShouldBeTrue();
        LibUsbWrapperMock.VerifyAll();
        LibUsbWrapperMock.VerifyNoOtherCalls();
        StreamerMock.VerifyAll();
        StreamerMock.VerifyNoOtherCalls();
        DiagnosticOutputRendererMock.VerifyAll();
    }

    [Fact]
    public void When_connecting_fails()
    {
        LibUsbWrapperMock
            .Setup(x => x.OpenDeviceWithVidPid(UsbContext, PushIdentity.Push2.VendorId, PushIdentity.Push2.ProductId))
            .Returns(IntPtr.Zero)
            .Verifiable(Times.Once);
        LibUsbWrapperMock
            .Setup(x => x.OpenDeviceWithVidPid(UsbContext, PushIdentity.Push3.VendorId, PushIdentity.Push3.ProductId))
            .Returns(IntPtr.Zero)
            .Verifiable(Times.Once);
        ClaimInterfaceSetup.Verifiable(Times.Never);

        var result = Sut.Connect();

        result.ShouldBeFalse();
        Sut.IsConnected.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => Sut.Disconnect()).Message.ShouldContain("not connected");
        Logger.LogMessages.Count.ShouldBeGreaterThanOrEqualTo(1);
        Logger.LogMessages.ShouldContain(x => x.Contains("Unable to find"));
        LibUsbWrapperMock.VerifyAll();
        LibUsbWrapperMock.VerifyNoOtherCalls();
        StreamerMock.VerifyAll();
        StreamerMock.VerifyNoOtherCalls();
        EventSourceMock.FrameCapturedHandlerAddCount.ShouldBe(0);
        EventSourceMock.FrameCapturedHandlerRemoveCount.ShouldBe(1);
    }

    [Fact]
    public void When_display_interface_is_not_found()
    {
        ClaimResult = -4;

        ReleaseInterfaceSetup.Verifiable(Times.Once);
        CloseSetup.Verifiable(Times.Once);
        ExitSetup.Verifiable(Times.Once);
        LibUsbWrapperMock
            .Setup(x => x.LibUsbErrorName(ClaimResult))
            .Returns(IntPtr.Zero);

        var result = Sut.Connect();

        result.ShouldBeFalse();
        Sut.IsConnected.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => Sut.Disconnect()).Message.ShouldContain("not connected");
        Logger.LogMessages.Count.ShouldBePositive();
        Logger.LogMessages.ShouldContain(x => x.Contains("Claim interface"));
        LibUsbWrapperMock.VerifyAll();
        LibUsbWrapperMock.VerifyNoOtherCalls();
        StreamerMock.VerifyAll();
        StreamerMock.VerifyNoOtherCalls();
        EventSourceMock.FrameCapturedHandlerAddCount.ShouldBe(0);
        EventSourceMock.FrameCapturedHandlerRemoveCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void When_disconnecting()
    {
        ReleaseInterfaceSetup.Verifiable(Times.Once);
        CloseSetup.Verifiable(Times.Once);
        ExitSetup.Verifiable(Times.Once);

        Sut.Connect();
        Sut.Disconnect();

        Sut.IsConnected.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => Sut.Disconnect()).Message.ShouldContain("not connected");
        LibUsbWrapperMock.VerifyAll();
        LibUsbWrapperMock.VerifyNoOtherCalls();
        StreamerMock.VerifyAll();
        StreamerMock.VerifyNoOtherCalls();
        EventSourceMock.FrameCapturedHandlerRemoveCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void When_already_connected()
    {
        Sut.Connect();
        var result = Sut.Connect();

        result.ShouldBeTrue();
        LibUsbWrapperMock.VerifyAll();
        LibUsbWrapperMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void When_reconnecting()
    {
        InitSetup.Verifiable(Times.Exactly(2));
        OpenDeviceWithVidPidSetup.Verifiable(Times.Exactly(2));
        ClaimInterfaceSetup.Verifiable(Times.Exactly(2));
        ReleaseInterfaceSetup.Verifiable(Times.Once);
        CloseSetup.Verifiable(Times.Once);
        ExitSetup.Verifiable(Times.Once);

        Sut.Connect();
        Sut.Disconnect();
        var result = Sut.Connect();

        result.ShouldBeTrue();
        EventSourceMock.FrameCapturedHandlerAddCount.ShouldBe(2);
        EventSourceMock.FrameCapturedHandlerRemoveCount.ShouldBe(1);
    }

    [Fact]
    public void When_disposing()
    {
        InitSetup.Verifiable(Times.Once);
        OpenDeviceWithVidPidSetup.Verifiable(Times.Once);
        ClaimInterfaceSetup.Verifiable(Times.Once);
        ReleaseInterfaceSetup.Verifiable(Times.Once);
        CloseSetup.Verifiable(Times.Once);
        ExitSetup.Verifiable(Times.Once);

        Sut.Connect();
        Sut.Dispose();

        Sut.IsConnected.ShouldBeFalse();
        Should.Throw<ObjectDisposedException>(() => Sut.Disconnect());
        Should.Throw<ObjectDisposedException>(() => Sut.Connect());
        LibUsbWrapperMock.VerifyAll();
        LibUsbWrapperMock.VerifyNoOtherCalls();
        EventSourceMock.FrameCapturedHandlerRemoveCount.ShouldBe(1);
    }

    [Fact]
    public void When_receiving_frame()
    {
        var bgraBytes = PtnshiftFinderTests.ReadTestImage("correct-region.png");

        int headerLength;
        var headerBytes = new byte[16];
        LibUsbWrapperMock
            .Setup(x => x.BulkTransfer(
                PushDevice, 0x01,
                It.IsAny<ReadOnlyMemory<byte>>(),
                16,
                out headerLength,
                It.IsAny<int>()))
            .Callback(delegate(IntPtr _, byte _, ReadOnlyMemory<byte> bytes, int _, ref int transferredBytes, int _)
            {
                headerBytes = bytes.ToArray();
                transferredBytes = bytes.Length;
            })
            .Returns(0)
            .Verifiable(Times.Once);

        int frameLength;
        List<byte[]> frameBytes = [];
        LibUsbWrapperMock
            .Setup(x => x.BulkTransfer(
                PushDevice, 0x01,
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.Is<int>(x => x > 512),
                out frameLength,
                It.IsAny<int>()))
            .Callback(delegate(IntPtr _, byte _, ReadOnlyMemory<byte> bytes, int _, ref int transferredBytes, int _)
            {
                var buffer = bytes.ToArray();
                frameBytes.Add(buffer);
                transferredBytes = bytes.Length;
            })
            .Returns(0)
            .Verifiable(Times.AtLeastOnce);

        DiagnosticOutputRendererMock.Reset();
        DiagnosticOutputRendererMock
            .Setup(x => x.SetText(Subsystem.FrameTransmission, "Connected", true))
            .Verifiable(Times.Once);
        DiagnosticOutputRendererMock
            .Setup(x => x.SetText(Subsystem.FrameTransmission, "Streaming", null))
            .Verifiable(Times.Once);
        Sut.Connect();
        EventSourceMock.InvokeFrameCaptured(FrameCaptureType.Region, new(bgraBytes));

        headerBytes.ShouldBe(Push2Usb.FrameHeader.ToArray());
        frameBytes[0][..8].ShouldBe([7, 244, 7, 248, 7, 244, 7, 248]); // Source: trust me bro
        LibUsbWrapperMock.VerifyAll();
        LibUsbWrapperMock.VerifyNoOtherCalls();
        DiagnosticOutputRendererMock.VerifyAll();
    }
}
