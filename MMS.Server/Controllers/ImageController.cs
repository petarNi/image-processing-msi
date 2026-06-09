using Microsoft.AspNetCore.Mvc;
using MMS.Server.Services;

namespace MMS.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageController : ControllerBase
    {
        private readonly ImageProcessingService _service;

        public ImageController(ImageProcessingService service)
        {
            _service = service;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok("Server radi!");
        }

        [HttpPost("invert")]
        public async Task<IActionResult> Invert(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest("Nema slike.");

            using var stream = image.OpenReadStream();

            var result = await _service.InvertImageAsync(stream);

            return File(result, "image/png");
        }
    }
}