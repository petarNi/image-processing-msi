using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using System.Text.Json;

namespace MMS.Server.Services.Msi
{
    public class MsiImageService
    {
        public async Task<byte[]> EncodeToMsiAsync(Stream imageStream, byte compression = 2)
        {
            using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(imageStream);

            byte[] hsvPixelBytes = ExtractHsvPixels(image);

            byte[] storedPixelBytes;
            object metadata;

            if (compression == 2)
            {
                storedPixelBytes = HuffmanCompression.Compress(
                    hsvPixelBytes,
                    out var huffmanCodes,
                    out int bitLength);

                metadata = new
                {
                    createdAt = DateTime.UtcNow,
                    format = "MSI",
                    author = "Petar Nikolic",
                    colorSpace = "HSV",
                    compression = "Huffman",
                    originalPixelLength = hsvPixelBytes.Length,
                    compressedPixelLength = storedPixelBytes.Length,
                    huffmanBitLength = bitLength,
                    huffmanCodes = huffmanCodes.ToDictionary(
                        x => x.Key.ToString(),
                        x => x.Value)
                };
            }
            else if (compression == 4)
            {
                storedPixelBytes = Downsample420Mpeg2(
                    hsvPixelBytes,
                    image.Width,
                    image.Height,
                    out int chromaWidth,
                    out int chromaHeight);

                metadata = new
                {
                    createdAt = DateTime.UtcNow,
                    format = "MSI",
                    author = "Petar Nikolic",
                    colorSpace = "HSV",
                    compression = "Downsampling 4:2:0 MPEG-2",
                    originalPixelLength = hsvPixelBytes.Length,
                    downsampledPixelLength = storedPixelBytes.Length,
                    chromaWidth,
                    chromaHeight
                };
            }
            else
            {
                throw new InvalidDataException("Podržane kompresije su 2 = Huffman i 4 = Downsampling MPEG-2.");
            }

            byte[] metaBytes = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(metadata));

            MsiHeader header = new()
            {
                Width = (uint)image.Width,
                Height = (uint)image.Height,
                Channels = 3,
                ColorSpace = 3,
                Compression = compression,
                MetaLength = (uint)metaBytes.Length,
                PixelLength = (uint)storedPixelBytes.Length
            };

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms, Encoding.UTF8, leaveOpen: true);

            writer.Write(Encoding.ASCII.GetBytes(header.Magic));
            writer.Write(header.Version);
            writer.Write(header.HeaderLength);
            writer.Write(header.Width);
            writer.Write(header.Height);
            writer.Write(header.Channels);
            writer.Write(header.ColorSpace);
            writer.Write(header.Compression);
            writer.Write(header.Reserved);
            writer.Write(header.MetaLength);
            writer.Write(header.PixelLength);

            writer.Write(metaBytes);
            writer.Write(storedPixelBytes);
            writer.Flush();

            byte[] withoutCrc = ms.ToArray();
            uint crc = Crc32.Compute(withoutCrc);

            writer.Write(crc);
            writer.Flush();

            return ms.ToArray();
        }

        public async Task<byte[]> DecodeFromMsiAsync(Stream msiStream)
        {
            using MemoryStream ms = new();
            await msiStream.CopyToAsync(ms);

            byte[] allBytes = ms.ToArray();

            if (allBytes.Length < 32)
                throw new InvalidDataException("MSI fajl je prekratak.");

            uint storedCrc = BitConverter.ToUInt32(allBytes, allBytes.Length - 4);
            byte[] withoutCrc = allBytes[..^4];
            uint calculatedCrc = Crc32.Compute(withoutCrc);

            if (storedCrc != calculatedCrc)
                throw new InvalidDataException("CRC32 provera nije prošla. MSI fajl je korumpiran.");

            using MemoryStream readMs = new(withoutCrc);
            using BinaryReader reader = new(readMs, Encoding.UTF8);

            string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));

            if (magic != "MSI0")
                throw new InvalidDataException("MSI magic nije validan.");

            ushort version = reader.ReadUInt16();
            ushort headerLen = reader.ReadUInt16();
            uint width = reader.ReadUInt32();
            uint height = reader.ReadUInt32();
            byte channels = reader.ReadByte();
            byte colorSpace = reader.ReadByte();
            byte compression = reader.ReadByte();
            reader.ReadByte();

            uint metaLen = reader.ReadUInt32();
            uint pixelLen = reader.ReadUInt32();

            if (version != 1)
                throw new InvalidDataException("Nepodržana MSI verzija.");

            if (headerLen != 28)
                throw new InvalidDataException("MSI header length nije validan.");

            if (channels != 3)
                throw new InvalidDataException("Trenutno je podržana slika sa 3 kanala.");

            if (colorSpace != 3)
                throw new InvalidDataException("Za neparan indeks očekuje se HSV color space.");

            long expectedFileLength = 28L + metaLen + pixelLen + 4L;

            if (allBytes.Length != expectedFileLength)
                throw new InvalidDataException("MSI dužina fajla nije validna.");

            byte[] metaBytes = reader.ReadBytes((int)metaLen);
            byte[] pixelBytes = reader.ReadBytes((int)pixelLen);

            using JsonDocument metaDoc = JsonDocument.Parse(metaBytes);

            byte[] hsvPixelBytes;

            if (compression == 2)
            {
                int originalPixelLength = metaDoc.RootElement
                    .GetProperty("originalPixelLength")
                    .GetInt32();

                int bitLength = metaDoc.RootElement
                    .GetProperty("huffmanBitLength")
                    .GetInt32();

                var codeElement = metaDoc.RootElement
                    .GetProperty("huffmanCodes");

                Dictionary<byte, string> huffmanCodes = new();

                foreach (var item in codeElement.EnumerateObject())
                {
                    huffmanCodes[byte.Parse(item.Name)] =
                        item.Value.GetString()!;
                }

                hsvPixelBytes = HuffmanCompression.Decompress(
                    pixelBytes,
                    huffmanCodes,
                    bitLength,
                    originalPixelLength);
            }
            else if (compression == 4)
            {
                int chromaWidth = metaDoc.RootElement
                    .GetProperty("chromaWidth")
                    .GetInt32();

                int chromaHeight = metaDoc.RootElement
                    .GetProperty("chromaHeight")
                    .GetInt32();

                hsvPixelBytes = Upsample420Mpeg2(
                    pixelBytes,
                    (int)width,
                    (int)height,
                    chromaWidth,
                    chromaHeight);
            }
            else
            {
                throw new InvalidDataException("Nepodržana MSI kompresija.");
            }

            int expectedPixelLength = checked((int)(width * height * channels));

            if (hsvPixelBytes.Length != expectedPixelLength)
                throw new InvalidDataException("Dužina dekodiranog pixel segmenta nije validna.");

            using Image<Rgba32> image = new((int)width, (int)height);

            int index = 0;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    byte h = hsvPixelBytes[index++];
                    byte s = hsvPixelBytes[index++];
                    byte v = hsvPixelBytes[index++];

                    image[x, y] = HsvToRgb(h, s, v);
                }
            }

            using MemoryStream output = new();
            await image.SaveAsPngAsync(output);

            return output.ToArray();
        }

        private static byte[] ExtractHsvPixels(Image<Rgba32> image)
        {
            byte[] pixels = new byte[image.Width * image.Height * 3];
            int index = 0;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 p = image[x, y];

                    RgbToHsv(p, out byte h, out byte s, out byte v);

                    pixels[index++] = h;
                    pixels[index++] = s;
                    pixels[index++] = v;
                }
            }

            return pixels;
        }

        private static void RgbToHsv(Rgba32 color, out byte hByte, out byte sByte, out byte vByte)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h;

            if (delta == 0)
            {
                h = 0;
            }
            else if (max == r)
            {
                h = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                h = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                h = 60 * (((r - g) / delta) + 4);
            }

            if (h < 0)
                h += 360;

            double s = max == 0 ? 0 : delta / max;
            double v = max;

            hByte = (byte)Math.Clamp((int)Math.Round(h / 360.0 * 255.0), 0, 255);
            sByte = (byte)Math.Clamp((int)Math.Round(s * 255.0), 0, 255);
            vByte = (byte)Math.Clamp((int)Math.Round(v * 255.0), 0, 255);
        }

        private static Rgba32 HsvToRgb(byte hByte, byte sByte, byte vByte)
        {
            double h = hByte / 255.0 * 360.0;
            double s = sByte / 255.0;
            double v = vByte / 255.0;

            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;

            double r1, g1, b1;

            if (h < 60)
            {
                r1 = c; g1 = x; b1 = 0;
            }
            else if (h < 120)
            {
                r1 = x; g1 = c; b1 = 0;
            }
            else if (h < 180)
            {
                r1 = 0; g1 = c; b1 = x;
            }
            else if (h < 240)
            {
                r1 = 0; g1 = x; b1 = c;
            }
            else if (h < 300)
            {
                r1 = x; g1 = 0; b1 = c;
            }
            else
            {
                r1 = c; g1 = 0; b1 = x;
            }

            byte r = (byte)Math.Clamp((int)Math.Round((r1 + m) * 255), 0, 255);
            byte g = (byte)Math.Clamp((int)Math.Round((g1 + m) * 255), 0, 255);
            byte b = (byte)Math.Clamp((int)Math.Round((b1 + m) * 255), 0, 255);

            return new Rgba32(r, g, b, 255);
        }

        private static byte[] Downsample420Mpeg2(
            byte[] hsvPixels,
            int width,
            int height,
            out int chromaWidth,
            out int chromaHeight)
        {
            chromaWidth = (width + 1) / 2;
            chromaHeight = (height + 1) / 2;

            byte[] hPlane = new byte[width * height];
            byte[] sPlane = new byte[chromaWidth * chromaHeight];
            byte[] vPlane = new byte[chromaWidth * chromaHeight];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sourceIndex = (y * width + x) * 3;
                    hPlane[y * width + x] = hsvPixels[sourceIndex];
                }
            }

            for (int by = 0; by < chromaHeight; by++)
            {
                for (int bx = 0; bx < chromaWidth; bx++)
                {
                    int sumS = 0;
                    int sumV = 0;
                    int count = 0;

                    for (int dy = 0; dy < 2; dy++)
                    {
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int x = bx * 2 + dx;
                            int y = by * 2 + dy;

                            if (x >= width || y >= height)
                                continue;

                            int sourceIndex = (y * width + x) * 3;

                            sumS += hsvPixels[sourceIndex + 1];
                            sumV += hsvPixels[sourceIndex + 2];
                            count++;
                        }
                    }

                    int chromaIndex = by * chromaWidth + bx;

                    sPlane[chromaIndex] = (byte)(sumS / count);
                    vPlane[chromaIndex] = (byte)(sumV / count);
                }
            }

            byte[] result = new byte[hPlane.Length + sPlane.Length + vPlane.Length];

            Buffer.BlockCopy(hPlane, 0, result, 0, hPlane.Length);
            Buffer.BlockCopy(sPlane, 0, result, hPlane.Length, sPlane.Length);
            Buffer.BlockCopy(vPlane, 0, result, hPlane.Length + sPlane.Length, vPlane.Length);

            return result;
        }

        private static byte[] Upsample420Mpeg2(
            byte[] downsampledPixels,
            int width,
            int height,
            int chromaWidth,
            int chromaHeight)
        {
            int hPlaneLength = width * height;
            int chromaLength = chromaWidth * chromaHeight;

            if (downsampledPixels.Length != hPlaneLength + chromaLength * 2)
                throw new InvalidDataException("Downsampled pixel segment nije validne dužine.");

            byte[] result = new byte[width * height * 3];

            int sOffset = hPlaneLength;
            int vOffset = hPlaneLength + chromaLength;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = y * width + x;
                    int chromaX = x / 2;
                    int chromaY = y / 2;
                    int chromaIndex = chromaY * chromaWidth + chromaX;

                    int targetIndex = pixelIndex * 3;

                    result[targetIndex] = downsampledPixels[pixelIndex];
                    result[targetIndex + 1] = downsampledPixels[sOffset + chromaIndex];
                    result[targetIndex + 2] = downsampledPixels[vOffset + chromaIndex];
                }
            }

            return result;
        }
    }
}