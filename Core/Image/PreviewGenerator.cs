using Avalonia.Media.Imaging;

namespace Core.Image;

public interface IPreviewGenerator
{
    Bitmap ConvertRawBytesToPng(ReadOnlySpan<byte> frame);
}

public class PreviewGenerator(
    IImageConverter imageConverter,
    IImageSaver imageSaver)
    : IPreviewGenerator
{
    private IImageConverter ImageConverter { get; } = imageConverter;
    private IImageSaver ImageSaver { get; } = imageSaver;

    private Random Rnd { get; } = new();

    public Bitmap ConvertRawBytesToPng(ReadOnlySpan<byte> frame)
    {
        var data = ImageConverter.ConvertToData(frame);

#if DEBUG
        if (Rnd.Next(0, 1000) == 0)
        {
            ImageSaver.SavePngToDisk(data);
        }
#endif

        return Bitmap.DecodeToWidth(data.AsStream(), 960);
    }
}