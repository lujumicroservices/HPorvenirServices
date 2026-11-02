using HPorvenir.Elastic;
using HPorvenir.Storage;
using HPorvenir.Web.Api.Model;
using Microsoft.Extensions.Configuration;
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
        private readonly ElasticOptions _elasticOptions;
        private readonly string _searchIndex;

        public PingController(IStorage storageProvider, IConfiguration configuration)
        {
            _storageProvider = storageProvider;
            _elasticOptions = IndexerSettingsLoader.Load().Elastic;
            _searchIndex = configuration["Elastic:SearchAlias"] ?? _elasticOptions.SearchAlias;
        }

        [HttpGet]
        public IActionResult Ping()
        {
            try {
                Navegation.Navegation navlogic = new Navegation.Navegation(_storageProvider);
                navlogic.LoadNavigation();

                SearchRequest searchRequest = new SearchRequest();
                searchRequest.Terms = new string[] { "casa" };
                Searcher searcher = new Searcher(_elasticOptions, _searchIndex);
                var result = searcher.Search(searchRequest.Terms, searchRequest.IsPhrase, searchRequest.StartDate, searchRequest.EndDate);

                return Ok(new {
                    nav = "ok",
                    search = "ok",
                    elasticIndex = _searchIndex,
                    elasticCloud = _elasticOptions.UseElasticCloud,
                    resultCount = result?.Count ?? 0
                });
            }
            catch (Exception ex){
                return StatusCode(500, new {
                    nav = "error",
                    search = "error",
                    elasticIndex = _searchIndex,
                    elasticCloud = _elasticOptions.UseElasticCloud,
                    error = ex.Message
                });
            }
        }

    }
}
