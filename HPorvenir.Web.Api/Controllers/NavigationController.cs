using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HPorvenir.Web.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NavigationController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }


        [HttpGet]
        public string Get()
        {
            Navegation.Navegation navlogic = new Navegation.Navegation();
            navlogic.LoadNavigation();


            return "";
        }
    }
}
