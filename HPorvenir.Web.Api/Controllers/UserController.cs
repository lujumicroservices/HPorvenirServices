using HPorvenir.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HPorvenir.Web.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class UserController : Controller
    {
        AuthManager _authManager = null;
        private readonly ILogger<UserController> _logger;

        public UserController(AuthManager authManager, ILogger<UserController> logger)
        {
            _authManager = authManager;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetUsers()
        {
            var result = _authManager.GetUsers();
            return Ok(result);
        }

        [HttpPost]
        public IActionResult AddUsers(HPorvenir.Model.User user)
        {
            if (_authManager.UserExistsByUser(user.UserName)) {
                StatusCode(500, new { errorMesage = "user Already exists", errorCode = 1000 });
            }
            var result = _authManager.AddUsers(user);
            return Ok(result);
        }

        [HttpPut]
        public IActionResult UpdateUsers(HPorvenir.Model.User user)
        {
            var result = _authManager.UpdateUsers(user);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public IActionResult deleteUsers(int id)
        {
            var result = _authManager.DeleteUsers(id);
            return Ok(result);
        }

    }
}
