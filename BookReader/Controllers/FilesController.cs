using BookReader.Storage;
using BookReader.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookReader.Controllers
{
    [ApiController]
    [Route("api/files")]
    public class FilesController : ControllerBase
    {
        private readonly IFileStorageService _storage;
        private readonly ILogger<FilesController> _logger;

        public FilesController(IFileStorageService storage, ILogger<FilesController> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        // POST /api/files/upload
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [Authorize(Policy = "Author")]
        
        public async Task<IActionResult> Upload(IFormFile? file)
        {
            if (file is null || file.Length == 0)
                return BadRequest("Файл пустой");

            using (var validationStream = file.OpenReadStream())
            {
                if (!UploadedBookFormatValidator.IsValid(file.FileName, validationStream))
                    return BadRequest("Допустимы только файлы FB2, EPUB, PDF и TXT. Расширение должно соответствовать содержимому файла.");
            }

            _logger.LogInformation("Получен файл {FileName}, размер {FileSize} байт", file.FileName, file.Length);

            try
            {
                await using var stream = file.OpenReadStream();
                var key = await _storage.UploadAsync(
                    stream, file.FileName, UploadedBookFormatValidator.GetContentType(file.FileName));

                _logger.LogInformation("Файл {FileName} загружен в S3 под ключом {StorageKey}", file.FileName, key);
                return Ok(new { key });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось загрузить файл {FileName} в S3-хранилище", file.FileName);
                return Problem(
                    title: "Не удалось загрузить файл",
                    detail: "Проверьте доступность S3-хранилища и повторите попытку.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }

        // GET /api/files/url?key=books/xxx-file.pdf
        [HttpGet("url")]
        [Authorize(Policy = "User")]
        public async Task<IActionResult> GetUrl([FromQuery] string key)
        {
            var url = await _storage.GetPresignedUrlAsync(key);
            return Ok(new { url });
        }

        // GET /api/files/download?key=books/xxx-file.pdf
        [HttpGet("download")]
        [Authorize(Policy = "User")]
        public async Task<IActionResult> Download([FromQuery] string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return BadRequest("Не указан ключ файла");

            var (content, contentType) = await _storage.DownloadAsync(key);
            var fileName = key[(key.LastIndexOf('/') + 1)..];

            return File(content, contentType, fileName);
        }
    }
}
