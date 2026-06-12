using MMS.Server.Services;
using MMS.Server.Services.Msi;
using System.Diagnostics;
using System.Text;

ImageProcessingService imageService = new();
MsiImageService msiService = new();

const int repetitions = 10;

string solutionRoot = FindSolutionRoot();
string goldenFolder = Path.Combine(solutionRoot, "golden");
string outputCsv = Path.Combine(solutionRoot, "benchmark-results.csv");

if (!Directory.Exists(goldenFolder))
{
    Console.WriteLine($"Golden folder nije pronađen: {goldenFolder}");
    return;
}

string[] imageFiles = Directory
    .GetFiles(goldenFolder)
    .Where(f =>
        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
        f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
    .ToArray();

if (imageFiles.Length == 0)
{
    Console.WriteLine("Nema slika u golden folderu.");
    return;
}

List<BenchmarkResult> results = new();

foreach (string imagePath in imageFiles)
{
    Console.WriteLine($"Testing: {Path.GetFileName(imagePath)}");

    await RunFilterBenchmark("invert", imagePath, s => imageService.InvertImageAsync(s), results);
    await RunFilterBenchmark("brightness", imagePath, s => imageService.BrightnessImageAsync(s, 50), results);
    await RunFilterBenchmark("pixelate", imagePath, s => imageService.PixelateImageAsync(s, 10), results);
    await RunFilterBenchmark("emboss-laplacian", imagePath, s => imageService.EmbossLaplacianAsync(s), results);
    await RunFilterBenchmark("edge-enhance", imagePath, s => imageService.EdgeEnhanceAsync(s), results);
    await RunFilterBenchmark("time-warp", imagePath, s => imageService.TimeWarpAsync(s, 15), results);
    await RunFilterBenchmark("jarvis-judice-ninke", imagePath, s => imageService.JarvisJudiceNinkeAsync(s), results);
    await RunFilterBenchmark("colorize-grayscale", imagePath, s => imageService.ColorizeGrayscaleAsync(s), results);

    await RunBatch3Benchmark(imagePath, results);

    await RunMsiBenchmark("msi-encode-huffman", imagePath, async s =>
    {
        return await msiService.EncodeToMsiAsync(s, compression: 2);
    }, results);

    await RunMsiBenchmark("msi-encode-mpeg2", imagePath, async s =>
    {
        return await msiService.EncodeToMsiAsync(s, compression: 4);
    }, results);

    await RunMsiDecodeBenchmark("msi-decode-huffman", imagePath, compression: 2, results);
    await RunMsiDecodeBenchmark("msi-decode-mpeg2", imagePath, compression: 4, results);
}

WriteCsv(results, outputCsv);

Console.WriteLine();
Console.WriteLine($"Benchmark završen.");
Console.WriteLine($"CSV rezultat: {outputCsv}");

async Task RunFilterBenchmark(
    string operation,
    string imagePath,
    Func<Stream, Task<byte[]>> action,
    List<BenchmarkResult> results)
{
    List<long> times = new();
    long outputSize = 0;

    for (int i = 0; i < repetitions; i++)
    {
        byte[] inputBytes = await File.ReadAllBytesAsync(imagePath);
        using MemoryStream inputStream = new(inputBytes);

        Stopwatch stopwatch = Stopwatch.StartNew();

        byte[] output = await action(inputStream);

        stopwatch.Stop();

        times.Add(stopwatch.ElapsedMilliseconds);
        outputSize = output.Length;
    }

    results.Add(BenchmarkResult.FromTimes(
        operation,
        Path.GetFileName(imagePath),
        times,
        new FileInfo(imagePath).Length,
        outputSize));
}

async Task RunBatch3Benchmark(
    string imagePath,
    List<BenchmarkResult> results)
{
    List<long> times = new();
    long outputSize = 0;

    for (int i = 0; i < repetitions; i++)
    {
        byte[] inputBytes = await File.ReadAllBytesAsync(imagePath);

        Stopwatch stopwatch = Stopwatch.StartNew();

        byte[] step1;
        byte[] step2;
        byte[] step3;

        using (MemoryStream s1 = new(inputBytes))
            step1 = await imageService.BrightnessImageAsync(s1, 50);

        using (MemoryStream s2 = new(step1))
            step2 = await imageService.InvertImageAsync(s2);

        using (MemoryStream s3 = new(step2))
            step3 = await imageService.PixelateImageAsync(s3, 10);

        stopwatch.Stop();

        times.Add(stopwatch.ElapsedMilliseconds);
        outputSize = step3.Length;
    }

    results.Add(BenchmarkResult.FromTimes(
        "batch-3-filters",
        Path.GetFileName(imagePath),
        times,
        new FileInfo(imagePath).Length,
        outputSize));
}

async Task RunMsiBenchmark(
    string operation,
    string imagePath,
    Func<Stream, Task<byte[]>> action,
    List<BenchmarkResult> results)
{
    List<long> times = new();
    long outputSize = 0;

    for (int i = 0; i < repetitions; i++)
    {
        byte[] inputBytes = await File.ReadAllBytesAsync(imagePath);
        using MemoryStream inputStream = new(inputBytes);

        Stopwatch stopwatch = Stopwatch.StartNew();

        byte[] output = await action(inputStream);

        stopwatch.Stop();

        times.Add(stopwatch.ElapsedMilliseconds);
        outputSize = output.Length;
    }

    results.Add(BenchmarkResult.FromTimes(
        operation,
        Path.GetFileName(imagePath),
        times,
        new FileInfo(imagePath).Length,
        outputSize));
}

async Task RunMsiDecodeBenchmark(
    string operation,
    string imagePath,
    byte compression,
    List<BenchmarkResult> results)
{
    byte[] originalBytes = await File.ReadAllBytesAsync(imagePath);

    byte[] msiBytes;

    using (MemoryStream originalStream = new(originalBytes))
        msiBytes = await msiService.EncodeToMsiAsync(originalStream, compression);

    List<long> times = new();
    long outputSize = 0;

    for (int i = 0; i < repetitions; i++)
    {
        using MemoryStream msiStream = new(msiBytes);

        Stopwatch stopwatch = Stopwatch.StartNew();

        byte[] output = await msiService.DecodeFromMsiAsync(msiStream);

        stopwatch.Stop();

        times.Add(stopwatch.ElapsedMilliseconds);
        outputSize = output.Length;
    }

    results.Add(BenchmarkResult.FromTimes(
        operation,
        Path.GetFileName(imagePath),
        times,
        msiBytes.Length,
        outputSize));
}

void WriteCsv(List<BenchmarkResult> results, string path)
{
    StringBuilder sb = new();

    sb.AppendLine("Operation,Image,AverageMs,MinMs,MaxMs,InputSizeBytes,OutputSizeBytes");

    foreach (BenchmarkResult result in results)
    {
        sb.AppendLine(
            $"{result.Operation},{result.Image},{result.AverageMs:F2},{result.MinMs},{result.MaxMs},{result.InputSizeBytes},{result.OutputSizeBytes}");
    }

    File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
}

string FindSolutionRoot()
{
    DirectoryInfo current = new(AppDomain.CurrentDomain.BaseDirectory);

    while (current != null)
    {
        if (current.GetFiles("*.sln").Any())
            return current.FullName;

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Nije pronađen solution root.");
}

class BenchmarkResult
{
    public string Operation { get; set; } = "";
    public string Image { get; set; } = "";
    public double AverageMs { get; set; }
    public long MinMs { get; set; }
    public long MaxMs { get; set; }
    public long InputSizeBytes { get; set; }
    public long OutputSizeBytes { get; set; }

    public static BenchmarkResult FromTimes(
        string operation,
        string image,
        List<long> times,
        long inputSize,
        long outputSize)
    {
        return new BenchmarkResult
        {
            Operation = operation,
            Image = image,
            AverageMs = times.Average(),
            MinMs = times.Min(),
            MaxMs = times.Max(),
            InputSizeBytes = inputSize,
            OutputSizeBytes = outputSize
        };
    }
}