using MMS.Server.Services;
using MMS.Server.Services.Msi;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace MMS.Server.Tests
{
    public class ImageProcessingServiceTests
    {
        private readonly ImageProcessingService _service = new();
        private readonly MsiImageService _msiService = new();

        private static async Task<MemoryStream> CreateTestImageStreamAsync()
        {
            using Image<Rgba32> image = new(64, 64);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    image[x, y] = new Rgba32(
                        (byte)(x * 4),
                        (byte)(y * 4),
                        120,
                        255);
                }
            }

            MemoryStream ms = new();
            await image.SaveAsPngAsync(ms);
            ms.Position = 0;

            return ms;
        }

        private static async Task AssertValidPngAsync(byte[] bytes)
        {
            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);

            using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(
                new MemoryStream(bytes));

            Assert.True(image.Width > 0);
            Assert.True(image.Height > 0);
        }

        [Fact]
        public async Task InvertImageAsync_ReturnsValidPng()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] result = await _service.InvertImageAsync(input);

            await AssertValidPngAsync(result);
        }

        [Fact]
        public async Task BrightnessImageAsync_ReturnsValidPng()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] result = await _service.BrightnessImageAsync(input, 50);

            await AssertValidPngAsync(result);
        }

        [Fact]
        public async Task PixelateImageAsync_ReturnsValidPng()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] result = await _service.PixelateImageAsync(input, 8);

            await AssertValidPngAsync(result);
        }

        [Fact]
        public async Task PixelateImageAsync_InvalidBlockSize_ThrowsException()
        {
            using var input = await CreateTestImageStreamAsync();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.PixelateImageAsync(input, 0));
        }

        [Fact]
        public async Task EmbossLaplacianAsync_ReturnsValidPng()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] result = await _service.EmbossLaplacianAsync(input);

            await AssertValidPngAsync(result);
        }

        [Fact]
        public async Task EdgeEnhanceAsync_ReturnsValidPng()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] result = await _service.EdgeEnhanceAsync(input);

            await AssertValidPngAsync(result);
        }

        [Fact]
        public async Task TimeWarpAsync_ReturnsValidPng()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] result = await _service.TimeWarpAsync(input, 15);

            await AssertValidPngAsync(result);
        }

        [Fact]
        public async Task JarvisJudiceNinkeAsync_ReturnsValidPng()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] result = await _service.JarvisJudiceNinkeAsync(input);

            await AssertValidPngAsync(result);
        }

        [Fact]
        public async Task ColorizeGrayscaleAsync_ReturnsValidPng()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] result = await _service.ColorizeGrayscaleAsync(input);

            await AssertValidPngAsync(result);
        }

        [Fact]
        public async Task MsiHuffman_EncodeDecode_ReturnsValidPng()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] msiBytes = await _msiService.EncodeToMsiAsync(input, compression: 2);

            using MemoryStream msiStream = new(msiBytes);

            byte[] pngBytes = await _msiService.DecodeFromMsiAsync(msiStream);

            await AssertValidPngAsync(pngBytes);
        }

        [Fact]
        public async Task MsiMpeg2_EncodeDecode_ReturnsValidPng()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] msiBytes = await _msiService.EncodeToMsiAsync(input, compression: 4);

            using MemoryStream msiStream = new(msiBytes);

            byte[] pngBytes = await _msiService.DecodeFromMsiAsync(msiStream);

            await AssertValidPngAsync(pngBytes);
        }

        [Fact]
        public async Task MsiCorruptedCrc_ThrowsInvalidDataException()
        {
            using var input = await CreateTestImageStreamAsync();

            byte[] msiBytes = await _msiService.EncodeToMsiAsync(input, compression: 2);

            msiBytes[10] = (byte)(msiBytes[10] ^ 0xFF);

            using MemoryStream corruptedStream = new(msiBytes);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                _msiService.DecodeFromMsiAsync(corruptedStream));
        }
    }
}