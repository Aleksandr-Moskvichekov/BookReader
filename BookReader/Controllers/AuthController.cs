using BookReader.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookReader.Recuests.Auth;

namespace BookReader.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                return Ok(await _auth.RegisterAsync(request));
            }
            catch (AppException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Message);
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                return Ok(await _auth.LoginAsync(request));
            }
            catch (AppException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Message);
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            try
            {
                return Ok(await _auth.RefreshAsync(request.RefreshToken));
            }
            catch (AppException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Message);
            }
        }

        // Защищённый эндпоинт-зеркало: показывает, кого видит сервер в токене
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new
            {
                UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                Role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
            });
        }
    }
}
