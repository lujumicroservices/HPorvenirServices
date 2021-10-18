using HPorvenir.Elastic;
using HPorvenir.Storage;
using HPorvenir.Web.Api.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HPorvenir.Web.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [AllowAnonymous]
    public class PingController : Controller
    {

        private readonly IStorage _storageProvider;

        public PingController(IStorage storageProvider)
        {
            _storageProvider = storageProvider;
        }

        [HttpGet]
        public IActionResult Ping()
        {
            try {
                //check navigation
                Navegation.Navegation navlogic = new Navegation.Navegation(_storageProvider);
                var data = navlogic.LoadNavigation();

                //check search
                SearchRequest searchRequest = new SearchRequest();
                searchRequest.Terms = new string[] { "casa" };
                Searcher searcher = new Searcher("hporvenir*");
                var result = searcher.Search(searchRequest.Terms, searchRequest.IsPhrase, searchRequest.StartDate, searchRequest.EndDate);

                return Ok(new { nav = "ok", search = "ok" });
            }
            catch{
                return StatusCode(500);
            }
            
        }

    }
}
