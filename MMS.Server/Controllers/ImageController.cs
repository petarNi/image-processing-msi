using Microsoft.AspNetCore.Mvc;
using MMS.Server.Services;
using MMS.Server.Helpers;
using MMS.Server.Models;
using System.Text.Json;
using MMS.Server.Services.Msi;

namespace MMS.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageController : ControllerBase
    {
        private readonly ImageProcessingService _service;
        private readonly MsiImageService _msiService;
        private readonly ILogger<ImageController> _logger;

        public ImageController(ImageProcessingService service, MsiImageService msiService, ILogger<ImageController> logger)
        {
            _service = service;
            _msiService = msiService;
            _logger = logger;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok("Server radi!");
        }

        [HttpPost("invert")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Invert(IFormFile image)
        {
            var validationError = ImageValidationHelper.ValidateImage(image);
            if (validationError != null)
            {
                _logger.LogWarning("Invalid Invert request: {Error}", validationError);
                return BadRequest(validationError);
            }

            _logger.LogInformation(
                "Filter Invert requested. File: {FileName}, Size: {FileSize} bytes",
                image.FileName,
                image.Length);

            using var stream = image.OpenReadStream();

            var result = await _service.InvertImageAsync(stream);

            _logger.LogInformation(
                "Filter Invert completed. File: {FileName}, OutputSize: {OutputSize} bytes",
                image.FileName,
                result.Length);

            return File(result, "image/png");
        }

        [HttpPost("brightness")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Brightness(IFormFile image, [FromForm] int amount = 50)
        {
            var validationError = ImageValidationHelper.ValidateImage(image);
            if (validationError != null)
            {
                _logger.LogWarning("Invalid Brightness request: {Error}", validationError);
                return BadRequest(validationError);
            }

            _logger.LogInformation(
                "Filter Brightness requested. File: {FileName}, Size: {FileSize} bytes, Amount: {Amount}",
                image.FileName,
                image.Length,
                amount);

            using var stream = image.OpenReadStream();

            var result =
                await _service.BrightnessImageAsync(stream, amount);

            _logger.LogInformation(
                "Filter Brightness completed. File: {FileName}, OutputSize: {OutputSize} bytes",
                image.FileName,
                result.Length);

            return File(result, "image/png");
        }

        [HttpPost("pixelate")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Pixelate(IFormFile image, [FromForm] int blockSize = 10)
        {
            var validationError = ImageValidationHelper.ValidateImage(image);
            if (validationError != null)
            {
                _logger.LogWarning("Invalid Pixelate request: {Error}", validationError);
                return BadRequest(validationError);
            }

            if (blockSize <= 0)
            {
                _logger.LogWarning("Invalid Pixelate request: blockSize={BlockSize}", blockSize);
                return BadRequest("blockSize mora biti veći od 0.");
            }

            _logger.LogInformation(
                "Filter Pixelate requested. File: {FileName}, Size: {FileSize} bytes",
                image.FileName,
                image.Length);

            using var stream = image.OpenReadStream();

            var result =
                await _service.PixelateImageAsync(stream, blockSize);

            _logger.LogInformation(
                "Filter Pixelate completed. File: {FileName}, OutputSize: {OutputSize} bytes",
                image.FileName,
                result.Length);

            return File(result, "image/png");
        }

        [HttpPost("emboss-laplacian")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> EmbossLaplacian(IFormFile image)
        {
            var validationError = ImageValidationHelper.ValidateImage(image);
            if (validationError != null)
            {
                _logger.LogWarning("Invalid EmbossLaplacian request: {Error}", validationError);
                return BadRequest(validationError);
            }

            _logger.LogInformation(
                "Filter EmbossLaplacian requested. File: {FileName}, Size: {FileSize} bytes",
                image.FileName,
                image.Length);

            using var stream = image.OpenReadStream();
            var result = await _service.EmbossLaplacianAsync(stream);

            _logger.LogInformation(
                "Filter EmbossLaplacian completed. File: {FileName}, OutputSize: {OutputSize} bytes",
                image.FileName,
                result.Length);

            return File(result, "image/png");
        }

        [HttpPost("edge-enhance")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> EdgeEnhance(IFormFile image)
        {
            var validationError = ImageValidationHelper.ValidateImage(image);
            if (validationError != null)
            {
                _logger.LogWarning("Invalid EdgeEnhance request: {Error}", validationError);
                return BadRequest(validationError);
            }

            _logger.LogInformation(
                "Filter EdgeEnhance requested. File: {FileName}, Size: {FileSize} bytes",
                image.FileName,
                image.Length);

            using var stream = image.OpenReadStream();
            var result = await _service.EdgeEnhanceAsync(stream);

            _logger.LogInformation(
                "Filter EdgeEnhance completed. File: {FileName}, OutputSize: {OutputSize} bytes",
                image.FileName,
                result.Length);

            return File(result, "image/png");
        }

        [HttpPost("time-warp")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> TimeWarp(IFormFile image, [FromForm] double factor = 15)
        {
            var validationError = ImageValidationHelper.ValidateImage(image);
            if (validationError != null)
            {
                _logger.LogWarning("Invalid TimeWarp request: {Error}", validationError);
                return BadRequest(validationError);
            }

            _logger.LogInformation(
                "Filter TimeWarp requested. File: {FileName}, Size: {FileSize} bytes, Factor: {Factor}",
                image.FileName,
                image.Length,
                factor);

            using var stream = image.OpenReadStream();
            var result = await _service.TimeWarpAsync(stream, factor);

            _logger.LogInformation(
                "Filter TimeWarp completed. File: {FileName}, OutputSize: {OutputSize} bytes",
                image.FileName,
                result.Length);

            return File(result, "image/png");
        }

        [HttpPost("jarvis-judice-ninke")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> JarvisJudiceNinke(IFormFile image)
        {
            var validationError = ImageValidationHelper.ValidateImage(image);
            if (validationError != null)
            {
                _logger.LogWarning("Invalid JarvisJudiceNinke request: {Error}", validationError);
                return BadRequest(validationError);
            }

            _logger.LogInformation(
                "Filter JarvisJudiceNinke requested. File: {FileName}, Size: {FileSize} bytes",
                image.FileName,
                image.Length);

            using var stream = image.OpenReadStream();
            var result = await _service.JarvisJudiceNinkeAsync(stream);

            _logger.LogInformation(
                "Filter JarvisJudiceNinke completed. File: {FileName}, OutputSize: {OutputSize} bytes",
                image.FileName,
                result.Length);

            return File(result, "image/png");
        }

        [HttpPost("colorize-grayscale")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ColorizeGrayscale(IFormFile image)
        {
            var validationError = ImageValidationHelper.ValidateImage(image);
            if (validationError != null)
            {
                _logger.LogWarning("Invalid ColorizeGrayscale request: {Error}", validationError);
                return BadRequest(validationError);
            }

            _logger.LogInformation(
                "Filter ColorizeGrayscale requested. File: {FileName}, Size: {FileSize} bytes",
                image.FileName,
                image.Length);

            using var stream = image.OpenReadStream();
            var result = await _service.ColorizeGrayscaleAsync(stream);

            _logger.LogInformation(
                "Filter ColorizeGrayscale completed. File: {FileName}, OutputSize: {OutputSize} bytes",
                image.FileName,
                result.Length);

            return File(result, "image/png");
        }

        [HttpPost("batch")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Batch(IFormFile image, [FromForm] List<string> filtersJson)
        {
            var validationError = ImageValidationHelper.ValidateImage(image);
            if (validationError != null)
            {
                _logger.LogWarning("Invalid Batch request: {Error}", validationError);
                return BadRequest(validationError);
            }

            if (filtersJson == null || filtersJson.Count == 0)
                return BadRequest("Lista filtera je prazna.");

            var filters = new List<FilterOperation>();

            try
            {
                foreach (var item in filtersJson)
                {
                    if (string.IsNullOrWhiteSpace(item))
                        continue;

                    _logger.LogInformation("RAW batch filter JSON: {FilterJson}", item);

                    var filter = JsonSerializer.Deserialize<FilterOperation>(
                        item,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (filter != null)
                        filters.Add(filter);
                }
            }
            catch
            {
                return BadRequest("filtersJson nije validan JSON.");
            }

            if (filters.Count == 0)
                return BadRequest("Mora postojati bar jedan filter.");

            _logger.LogInformation(
                "Batch requested. File: {FileName}, Size: {FileSize} bytes, Filters: {FilterCount}",
                image.FileName,
                image.Length,
                filters.Count);

            await using var inputStream = image.OpenReadStream();
            using MemoryStream currentImage = new();
            await inputStream.CopyToAsync(currentImage);

            byte[] currentBytes = currentImage.ToArray();

            foreach (var filter in filters)
            {
                if (string.IsNullOrWhiteSpace(filter.Name))
                    return BadRequest("Filter nema naziv.");

                await using MemoryStream stepStream = new(currentBytes);

                switch (filter.Name.ToLowerInvariant())
                {
                    case "invert":
                        currentBytes = await _service.InvertImageAsync(stepStream);
                        break;

                    case "brightness":
                        currentBytes = await _service.BrightnessImageAsync(
                            stepStream,
                            filter.Amount ?? 50);
                        break;

                    case "pixelate":
                        int blockSize = filter.BlockSize ?? 10;

                        if (blockSize <= 0)
                            return BadRequest("blockSize mora biti veći od 0.");

                        currentBytes = await _service.PixelateImageAsync(
                            stepStream,
                            blockSize);
                        break;

                    case "emboss-laplacian":
                        currentBytes = await _service.EmbossLaplacianAsync(stepStream);
                        break;

                    case "edge-enhance":
                        currentBytes = await _service.EdgeEnhanceAsync(stepStream);
                        break;

                    case "time-warp":
                        currentBytes = await _service.TimeWarpAsync(
                            stepStream,
                            filter.Factor ?? 15);
                        break;

                    case "jarvis-judice-ninke":
                        currentBytes = await _service.JarvisJudiceNinkeAsync(stepStream);
                        break;

                    case "colorize-grayscale":
                        currentBytes = await _service.ColorizeGrayscaleAsync(stepStream);
                        break;

                    default:
                        return BadRequest($"Nepoznat filter: {filter.Name}");
                }

                _logger.LogInformation("Batch filter applied: {FilterName}", filter.Name);
            }

            _logger.LogInformation(
                "Batch completed. File: {FileName}, OutputSize: {OutputSize} bytes",
                image.FileName,
                currentBytes.Length);

            return File(currentBytes, "image/png");
        }

        [HttpPost("export-msi")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ExportMsi(
    IFormFile image,
    [FromForm] byte compression = 2)
        {
            var validationError = ImageValidationHelper.ValidateImage(image);

            if (validationError != null)
            {
                _logger.LogWarning("Invalid ExportMsi request: {Error}", validationError);
                return BadRequest(validationError);
            }

            if (compression != 2 && compression != 4)
            {
                return BadRequest("Podržane kompresije su: 2 = Huffman, 4 = Downsampling 4:2:0 MPEG-2.");
            }

            _logger.LogInformation(
                "MSI export requested. File: {FileName}, Compression: {Compression}",
                image.FileName,
                compression);

            using var stream = image.OpenReadStream();

            var msiBytes = await _msiService.EncodeToMsiAsync(stream, compression);

            _logger.LogInformation(
                "MSI export completed. Size: {Size}, Compression: {Compression}",
                msiBytes.Length,
                compression);

            string fileName = Path.GetFileNameWithoutExtension(image.FileName);

            return File(
                msiBytes,
                "application/octet-stream",
                $"{fileName}.msi");
        }

        [HttpPost("import-msi")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportMsi(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("MSI fajl nije poslat.");

            if (Path.GetExtension(file.FileName).ToLowerInvariant() != ".msi")
                return BadRequest("Dozvoljeni su samo .msi fajlovi.");

            try
            {
                _logger.LogInformation(
                    "MSI import requested. File: {FileName}",
                    file.FileName);

                using var stream = file.OpenReadStream();

                var pngBytes = await _msiService.DecodeFromMsiAsync(stream);

                _logger.LogInformation(
                    "MSI import completed. Output size: {Size}",
                    pngBytes.Length);

                string fileName = Path.GetFileNameWithoutExtension(file.FileName);

                return File(
                    pngBytes,
                    "image/png",
                    $"{fileName}.png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "MSI import failed.");

                return BadRequest(ex.Message);
            }
        }
    }
}