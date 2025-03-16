namespace Tests.GUI;

public static class ImageGenerator
{
    public static byte[] GenerateTestBitmap(int overallWidth, int overallHeight, int posX, int posY)
    {
        var data = new byte[overallWidth * overallHeight * 4];

        // Fill the entire image with blue (BGRA: Blue=0xFF, Green=0x00, Red=0x00, Alpha=0xFF)
        for (var y = 0; y < overallHeight; y++)
        {
            for (var x = 0; x < overallWidth; x++)
            {
                var offset = (y * overallWidth + x) * 4;
                data[offset + 0] = 0xFF; // Blue
                data[offset + 1] = 0x00; // Green
                data[offset + 2] = 0x00; // Red
                data[offset + 3] = 0xFF; // Alpha
            }
        }

        if (posX < 0 || posY < 0)
        {
            return data;
        }

        // Define sub-rectangle dimensions and pattern
        const int RectWidth = 960;
        const int RectHeight = 161;
        const string Pattern = "abaabbbaaaabbbbb"; // 15 characters

        // Take posX and posY into account so that sub-rectangle is only as big as can fit in the overall image
        var effectiveWidth = Math.Min(RectWidth, overallWidth - posX);
        var effectiveHeight = Math.Min(RectHeight, overallHeight - posY);

        // Overlay the sub-rectangle
        for (var subY = 0; subY < effectiveHeight; subY++)
        {
            var imageY = posY + subY;
            if (imageY < 0 || imageY >= overallHeight)
                continue; // Skip rows outside the overall image

            for (var subX = 0; subX < effectiveWidth; subX++)
            {
                var imageX = posX + subX;
                if (imageX < 0 || imageX >= overallWidth)
                    continue; // Skip columns outside the overall image

                var offset = (imageY * overallWidth + imageX) * 4;

                if (subY == 0)
                {
                    // First row of sub-rectangle: apply test pattern
                    if (subX < Pattern.Length)
                    {
                        var c = Pattern[subX];
                        if (c == 'a')
                        {
                            // Color #1C1C1C in BGRA: 0x1C, 0x1C, 0x1C, 0xFF
                            data[offset + 0] = 0x1C;
                            data[offset + 1] = 0x1C;
                            data[offset + 2] = 0x1C;
                        }
                        else // 'b'
                        {
                            // Color #2C2C2C in BGRA: 0x2C, 0x2C, 0x2C, 0xFF
                            data[offset + 0] = 0x2C;
                            data[offset + 1] = 0x2C;
                            data[offset + 2] = 0x2C;
                        }
                    }
                    else
                    {
                        // Remaining pixels in the first row are black (#000000)
                        data[offset + 0] = 0x00;
                        data[offset + 1] = 0x00;
                        data[offset + 2] = 0x00;
                    }
                }
                else
                {
                    // Rows 1 to 160 of sub-rectangle: fill with green (#00FF00 in BGRA: 0x00, 0xFF, 0x00, 0xFF)
                    data[offset + 0] = 0x00;
                    data[offset + 1] = 0xFF;
                    data[offset + 2] = 0x00;
                }

                // All pixels are fully opaque
                data[offset + 3] = 0xFF;
            }
        }

        return data;
    }
}