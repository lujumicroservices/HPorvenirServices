using HPorvenir.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HPorvenir.Web.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class NavigationController : Controller
    {
        private readonly IStorage _storageProvider;
        public NavigationController(IStorage storageProvider) {
            _storageProvider = storageProvider;
        }


        [HttpGet]
        public IActionResult Get()
        {
            Navegation.Navegation navlogic = new Navegation.Navegation();
            var data = navlogic.LoadNavigation();
            return Ok(data);
        }


        [HttpGet("day/{year}/{month}/{day}")]
        public IActionResult GetDay(int year, int month, int day)
        {
            
            var results = _storageProvider.ListDay(year, month, day);
            return Ok(results);
        }

        [HttpGet("file/{pathId}")]
        public IActionResult GetFile(string pathId)
        {

            var results = _storageProvider.ReadAsync(pathId);
            return Ok(results);
        }
    }
}
