using HPorvenir.Authentication;
using HPorvenir.Elastic;
using HPorvenir.Storage;
using HPorvenir.Web.Api.Model;
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
    public class AuthenticationController : Controller
    {

        AuthManager _authManager = null;
        private readonly ILogger<AuthenticationController> _logger;



        public AuthenticationController(AuthManager authManager, ILogger<AuthenticationController> logger) {
            _authManager = authManager;
            _logger = logger;
        }


        [HttpPost("login")]
        public IActionResult login(Login login)
        {
            var user = _authManager.VerifyUser(login.User, login.Password);
            var token = _authManager.GenerateToken(user);
            return Ok(new { user = user, access_token = token });
        }

        [HttpGet("iplogin")]
        [AllowAnonymous]
        public IActionResult IpLogin()
        {

            var ip = HttpContext.Connection.RemoteIpAddress;
            
            return Ok(HttpContext.Request.Headers.Select(x => x.Value).ToList());
        }


        [HttpGet("accesstoken")]
        [Authorize]
        public IActionResult accessToken()
        {
            var user = _authManager.GetUset(HttpContext.User.Identity.Name);
            var token = _authManager.GenerateToken(user);
            return Ok(new { user = user, access_token = token });            
        }



    }

}
