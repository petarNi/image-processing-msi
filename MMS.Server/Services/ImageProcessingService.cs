using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MMS.Server.Services
{
    public class ImageProcessingService
    {
        public async Task<byte[]> InvertImageAsync(Stream imageStream)
        {
            using Image<Rgba32> image =
                await Image.LoadAsync<Rgba32>(imageStream);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];

                    pixel.R = (byte)(255 - pixel.R);
                    pixel.G = (byte)(255 - pixel.G);
                    pixel.B = (byte)(255 - pixel.B);

                    image[x, y] = pixel;
                }
            }

            using MemoryStream ms = new();

            await image.SaveAsPngAsync(ms);

            return ms.ToArray();
        }
    }
}