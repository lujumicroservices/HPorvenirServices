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
            try
            {
                var user = _authManager.VerifyUser(login.User, login.Password);
                var token = _authManager.GenerateToken(user);
                return Ok(new { user = user, access_token = token });
            }
            catch (Exception ex) {
                return Ok(new { error = new List<Error> { new Error { Type = "manual", Message = ex.Message } } });
            }
            
            
            
        }

        [HttpGet("iplogin/{ipAddress}")]
        [AllowAnonymous]
        public IActionResult IpLogin(string ipAddress)
        {
            _logger.LogInformation(@$"try login by ip from ip {ipAddress}");

            var configip = new List<string> {
                "2806:103e:1b:cea:b514:a964:731b:600",
                "187.160.241.65",
                "201.96.108.213",
                "201.117.175.102",
                "201.134.33.209",
                "207.248.45.178",
                "189.219.114.161",
                "2806:230:5014:bf4c:29e2:b0d7:4",
                "2806:230:5014:bf4c:a070:42ae:f045:4b96"

            };

            string user = "";
            string pass = "";

            var tipo = 0;

            if (configip.Contains(ipAddress)) {
                tipo = 1;
            }

            if (ipAddress.StartsWith("148.234") || ipAddress.StartsWith("148.210") || ipAddress== "200.52.15.206" || ipAddress == "200.57.7.105" || ipAddress == "200.67.0.182") {
                tipo = 2;
            }

            if (ipAddress.StartsWith("201.144.14")) {
                tipo = 2;
            }

            if (ipAddress.StartsWith("189.254.215"))
            {
                tipo = 2;
            }
            if (ipAddress.StartsWith("201.147.197"))
            {
                tipo = 2;
            }


            if (tipo == 1)
            {
                user = "hemeroteca";
                pass = "dgxusrh";
            }
            else if (tipo == 2)
            {
                user = "uanl";
                pass = "uanldgx12";
            }
            else {
                return Unauthorized();
            }

            var userObj = _authManager.VerifyUser(user, pass);
            var token = _authManager.GenerateToken(userObj);
            return Ok(new { user = user, access_token = token });            
        }

        [HttpGet("remotelogin/{secret}/{iv}")]
        [AllowAnonymous]
        public IActionResult Remote(string secret, string iv)
        {
            _logger.LogInformation(@$"try log remote {secret} - {iv}");

            string key = "vOVH6sdmpNWjRRIqCc7rdxs01lwHzfr3";
            var text = Encryption.DecriptCRT(key, iv, secret);
            var data = text.Split("@");
            try {
                if (data.Length == 2)
                {
                    if (double.Parse(data[1]) > DateTime.Now.Millisecond - 10000)
                    {
                        return IpLogin(data[0]);
                    }
                }

            }
            catch  {
                return Unauthorized();
            }                       
            return Unauthorized();
        }
      

        //[HttpGet("diagnostico")]
        //[AllowAnonymous]
        //public IActionResult diagnostico()
        //{
        //    var user = _authManager.GetUset(HttpContext.User.Identity.Name);
        //    var token = _authManager.GenerateToken(user);
        //    return Ok(new { user = user, access_token = token });
        //}


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
