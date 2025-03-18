namespace Core.Usb;

public record PushIdentity(
    ushort VendorId,
    ushort ProductId,
    byte DisplayInterface,
    byte DisplayEndpoint)
{
    public static PushIdentity Push2 { get; } = new(
        0x2982,
        0x1967,
        0,
        0x01);

    public static PushIdentity Push3 { get; } = new(
        0x2982,
        0x1969,
        0,
        0x01);
}