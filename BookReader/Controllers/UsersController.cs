using BookReader.Recuests;
using BookReader.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookReader.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly UserSevice userSevice; 
        public UsersController(UserSevice userMi) //пробник
        {
            userSevice = userMi;
        }

        [HttpPost()]
        public IActionResult Index([FromBody] GetData mim)
        {
            int res = userSevice.IMultiplyOperators(mim.A, mim.B);
            return Ok(res);

        }

        
    }
}
