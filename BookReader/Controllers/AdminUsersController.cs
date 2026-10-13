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
