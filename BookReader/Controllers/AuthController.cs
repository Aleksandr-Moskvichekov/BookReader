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

        /// <summary>Регистрирует пользователя и возвращает access- и refresh-токены.</summary>
        /// <remarks>Роль нового пользователя — User. Email должен быть уникальным, пароль — не короче 6 символов.</remarks>
        [HttpPost("register")]
        [Authorize(Policy = "Guest")]
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

        /// <summary>Выполняет вход по email и паролю.</summary>
        /// <remarks>При успешной проверке возвращает JWT access token и refresh token.</remarks>
        [HttpPost("login")]
        [Authorize(Policy = "Guest")]
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

        /// <summary>Обновляет пару токенов по refresh token.</summary>
        /// <remarks>Использованный refresh token отзывается; в ответе выдаётся новая пара токенов.</remarks>
        [HttpPost("refresh")]
        [Authorize(Policy = "Guest")]
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

        /// <summary>Завершает сессию, отзывая refresh token.</summary>
        /// <remarks>Access token остаётся действительным до истечения срока или изменения token version пользователя.</remarks>
        [HttpPost("logout")]
        [Authorize(Policy = "Guest")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            await _auth.LogoutAsync(request.RefreshToken);
            return NoContent();
        }

        // Защищённый эндпоинт-зеркало: показывает, кого видит сервер в токене
        /// <summary>Возвращает идентификатор, email и роль текущего пользователя из JWT.</summary>
        /// <remarks>Требуется авторизация с ролью User, Author или Admin.</remarks>
        [Authorize(Policy = "User")]
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
