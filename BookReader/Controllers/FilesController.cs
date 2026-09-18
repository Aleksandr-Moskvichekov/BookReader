using BookReader.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookReader.Controllers
{
    [ApiController]
    [Route("api/files")]
    public class FilesController : ControllerBase
    {
        private readonly IFileStorageService _storage;

        public FilesController(IFileStorageService storage)
        {
            _storage = storage;
        }

        // POST /api/files/upload
        [HttpPost("upload")] // поправить код upload не работает из-за AntyFrogeryToken
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file is null || file.Length == 0)
                return BadRequest("Файл пустой");

            using var stream = file.OpenReadStream();
            var key = await _storage.UploadAsync(
                stream, file.FileName, file.ContentType ?? "application/octet-stream");

            return Ok(new { key });
        }

        // GET /api/files/url?key=books/xxx-file.pdf
        [HttpGet("url")]
        
        public async Task<IActionResult> GetUrl([FromQuery] string key)
        {
            var url = await _storage.GetPresignedUrlAsync(key);
            return Ok(new { url });
        }
    }
}
