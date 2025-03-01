namespace Core.Usb;

public record Push2Identity(
    ushort VendorId,
    ushort ProductId,
    byte DisplayInterface,
    byte DisplayEndpoint)
{
    public static Push2Identity Standard { get; } = new(
        0x2982,
        0x1967,
        0,
        0x01);
}