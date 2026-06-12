using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MMS.Server.Services
{
    public class ImageProcessingService
    {
        private static byte ClampToByte(double value)
            => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

        public async Task<byte[]> InvertImageAsync(Stream imageStream)
        {
            using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(imageStream);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        row[x].R = (byte)(255 - row[x].R);
                        row[x].G = (byte)(255 - row[x].G);
                        row[x].B = (byte)(255 - row[x].B);
                    }
                }
            });

            using MemoryStream ms = new();
            await image.SaveAsPngAsync(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> BrightnessImageAsync(Stream imageStream, int amount)
        {
            using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(imageStream);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        row[x].R = ClampToByte(row[x].R + amount);
                        row[x].G = ClampToByte(row[x].G + amount);
                        row[x].B = ClampToByte(row[x].B + amount);
                    }
                }
            });

            using MemoryStream ms = new();
            await image.SaveAsPngAsync(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> PixelateImageAsync(Stream imageStream, int blockSize)
        {
            if (blockSize <= 0)
                throw new ArgumentException("blockSize mora biti veći od 0.");

            using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(imageStream);

            for (int y = 0; y < image.Height; y += blockSize)
            {
                for (int x = 0; x < image.Width; x += blockSize)
                {
                    Rgba32 pixel = image[x, y];

                    for (int yy = y; yy < Math.Min(y + blockSize, image.Height); yy++)
                    {
                        for (int xx = x; xx < Math.Min(x + blockSize, image.Width); xx++)
                        {
                            image[xx, yy] = pixel;
                        }
                    }
                }
            }

            using MemoryStream ms = new();
            await image.SaveAsPngAsync(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> EmbossLaplacianAsync(Stream imageStream)
        {
            int[,] kernel =
            {
                { -1, -1, -1 },
                { -1,  8, -1 },
                { -1, -1, -1 }
            };

            return await ApplyKernelAsync(imageStream, kernel, factor: 1, bias: 128);
        }

        public async Task<byte[]> EdgeEnhanceAsync(Stream imageStream)
        {
            int[,] kernel =
            {
                {  0,  0,  0 },
                { -1,  1,  0 },
                {  0,  0,  0 }
            };

            return await ApplyKernelAsync(imageStream, kernel, factor: 1, bias: 0);
        }

        private async Task<byte[]> ApplyKernelAsync(Stream imageStream, int[,] kernel, double factor, double bias)
        {
            using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(imageStream);
            using Image<Rgba32> copy = image.Clone();

            int width = image.Width;
            int height = image.Height;

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    double r = 0, g = 0, b = 0;

                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            Rgba32 p = copy[x + kx, y + ky];
                            int k = kernel[ky + 1, kx + 1];

                            r += p.R * k;
                            g += p.G * k;
                            b += p.B * k;
                        }
                    }

                    image[x, y] = new Rgba32(
                        ClampToByte(factor * r + bias),
                        ClampToByte(factor * g + bias),
                        ClampToByte(factor * b + bias),
                        copy[x, y].A
                    );
                }
            }

            using MemoryStream ms = new();
            await image.SaveAsPngAsync(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> TimeWarpAsync(Stream imageStream, double factor = 15)
        {
            using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(imageStream);
            using Image<Rgba32> copy = image.Clone();

            int width = image.Width;
            int height = image.Height;

            for (int y = 0; y < height; y++)
            {
                double offset = Math.Sin(2 * Math.PI * y / 128.0) * factor;

                for (int x = 0; x < width; x++)
                {
                    int sourceX = Math.Clamp((int)(x + offset), 0, width - 1);
                    image[x, y] = copy[sourceX, y];
                }
            }

            using MemoryStream ms = new();
            await image.SaveAsPngAsync(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> JarvisJudiceNinkeAsync(Stream imageStream)
        {
            using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(imageStream);

            double[,] gray = new double[image.Width, image.Height];

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 p = image[x, y];
                    gray[x, y] = 0.299 * p.R + 0.587 * p.G + 0.114 * p.B;
                }
            }

            int[,] matrix =
            {
                { 0, 0, 0, 7, 5 },
                { 3, 5, 7, 5, 3 },
                { 1, 3, 5, 3, 1 }
            };

            int divisor = 48;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    double oldPixel = gray[x, y];
                    double newPixel = oldPixel < 128 ? 0 : 255;
                    double error = oldPixel - newPixel;

                    byte value = (byte)newPixel;
                    image[x, y] = new Rgba32(value, value, value);

                    for (int dy = 0; dy < 3; dy++)
                    {
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int weight = matrix[dy, dx + 2];
                            if (weight == 0) continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            if (nx >= 0 && nx < image.Width && ny >= 0 && ny < image.Height)
                            {
                                gray[nx, ny] += error * weight / divisor;
                                gray[nx, ny] = Math.Clamp(gray[nx, ny], 0, 255);
                            }
                        }
                    }
                }
            }

            using MemoryStream ms = new();
            await image.SaveAsPngAsync(ms);
            return ms.ToArray();
        }

        public async Task<byte[]> ColorizeGrayscaleAsync(Stream imageStream)
        {
            using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(imageStream);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);

                    for (int x = 0; x < row.Length; x++)
                    {
                        byte gray = (byte)(0.299 * row[x].R + 0.587 * row[x].G + 0.114 * row[x].B);

                        row[x].R = gray;
                        row[x].G = ClampToByte(gray * 0.75);
                        row[x].B = ClampToByte(255 - gray * 0.5);
                    }
                }
            });

            using MemoryStream ms = new();
            await image.SaveAsPngAsync(ms);
            return ms.ToArray();
        }
    }
}