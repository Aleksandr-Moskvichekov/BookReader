using System.Security.Claims;
using BookReader.Recuests.Admin;
using BookReader.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookReader.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Policy = "Admin")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAdminUserService _adminUsers;

        public AdminUsersController(IAdminUserService adminUsers)
        {
            _adminUsers = adminUsers;
        }

        /// <summary>Возвращает страницу пользователей с необязательными фильтрами.</summary>
        /// <remarks>Доступно только Admin. Поддерживаются поиск, фильтр по роли и статусу блокировки, а также page/pageSize.</remarks>
        [HttpGet]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? search,
            [FromQuery] string? role,
            [FromQuery] bool? isBanned,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                return Ok(await _adminUsers.GetUsersAsync(search, role, isBanned, page, pageSize));
            }
            catch (AppException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Message);
            }
        }

        /// <summary>Возвращает сведения об одном пользователе.</summary>
        /// <remarks>Доступно только Admin.</remarks>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                return Ok(await _adminUsers.GetUserAsync(id));
            }
            catch (AppException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Message);
            }
        }

        /// <summary>Изменяет роль пользователя.</summary>
        /// <remarks>Доступно только Admin. Изменение роли отзывает ранее выданные токены пользователя.</remarks>
        [HttpPut("{id:int}/role")]
        public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeUserRoleRequest request)
        {
            try
            {
                return Ok(await _adminUsers.ChangeRoleAsync(id, request.Role, GetAdminUserId()));
            }
            catch (AppException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Message);
            }
        }

        /// <summary>Блокирует пользователя с указанной причиной.</summary>
        /// <remarks>Доступно только Admin. Активные токены заблокированного пользователя становятся недействительными.</remarks>
        [HttpPost("{id:int}/ban")]
        public async Task<IActionResult> Ban(int id, [FromBody] BanUserRequest request)
        {
            try
            {
                return Ok(await _adminUsers.BanAsync(id, request.Reason, GetAdminUserId()));
            }
            catch (AppException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Message);
            }
        }

        /// <summary>Снимает блокировку с пользователя.</summary>
        /// <remarks>Доступно только Admin.</remarks>
        [HttpPost("{id:int}/unban")]
        public async Task<IActionResult> Unban(int id)
        {
            try
            {
                return Ok(await _adminUsers.UnbanAsync(id));
            }
            catch (AppException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Message);
            }
        }

        private int GetAdminUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userId, out var parsedId))
                return parsedId;

            throw new AppException(401, "В JWT отсутствует корректный идентификатор пользователя");
        }
    }
}
