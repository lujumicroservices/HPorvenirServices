using HPorvenir.Document;
using HPorvenir.Elastic;
using HPorvenir.Storage;
using HPorvenir.Web.Api.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HPorvenir.Web.Api.Controllers
{

    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class SearchController : Controller
    {
        private readonly IStorage _storageProvider;
        private readonly ILogger<SearchController> _logger;

        public SearchController(IStorage storageProvider, ILogger<SearchController> logger)
        {
            _storageProvider = storageProvider;
            _logger = logger;
        }



        [HttpPost("simple")]        
        public IActionResult Search(SearchRequest searchRequest)
        {
            Searcher searcher = new Searcher("hporvenir*");
            var result = searcher.Search(searchRequest.Terms,searchRequest.IsPhrase, searchRequest.StartDate, searchRequest.EndDate);
            return Ok(result);
        }



        [HttpPost("file")]
        public async Task<ActionResult> FileInfoAsync(SearchRequest searchRequest)
        {
            Searcher searcher = new Searcher("hporvenir*");
            _logger.LogDebug("search {@searchRequest}", searchRequest);
            var resultHits = searcher.FileDetails(searchRequest.FileName, searchRequest.Terms, searchRequest.IsPhrase, searchRequest.StartDate, searchRequest.EndDate);
            Stream fileStream = null;
            try
            {
                fileStream = await _storageProvider.ReadAsync(searchRequest.FileName);
            }
            catch (Exception ex) {

                return StatusCode(500, new { error = "Archivo no encontrado, el problema fue reportado automaticamente al administrador", code = 1000 });
            }
            
            PDFDocument doc = new PDFDocument();

            var isAdmin = HttpContext.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role);

            var pdfStream =  doc.ProcessFile(fileStream, resultHits);
           


            return new FileStreamResult(pdfStream, new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"));
        }
    }
}
