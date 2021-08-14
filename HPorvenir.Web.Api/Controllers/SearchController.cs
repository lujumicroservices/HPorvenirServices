using HPorvenir.Document;
using HPorvenir.Elastic;
using HPorvenir.Storage;
using HPorvenir.Web.Api.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HPorvenir.Web.Api.Controllers
{

    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class SearchController : Controller
    {
        private readonly IStorage _storageProvider;
        public SearchController(IStorage storageProvider)
        {
            _storageProvider = storageProvider;
        }



        [HttpPost("simple")]        
        public IActionResult Search(SearchRequest searchRequest)
        {
            Searcher searcher = new Searcher("hporvenir*");
            var result = searcher.Search(searchRequest.Terms,searchRequest.IsPhrase, searchRequest.StartDate, searchRequest.EndDate);
            return Ok(result);
        }



        [HttpPost("file")]
        public async Task<FileStreamResult> FileInfoAsync(SearchRequest searchRequest)
        {
            Searcher searcher = new Searcher("hporvenir*");
            var resultHits = searcher.FileDetails(searchRequest.FileName, searchRequest.Terms, searchRequest.IsPhrase, searchRequest.StartDate, searchRequest.EndDate);
            var fileStream = await  _storageProvider.ReadAsync(searchRequest.FileName);
            PDFDocument doc = new PDFDocument();
            var pdfStream =  doc.ProcessFile(fileStream, resultHits);
           
            return new FileStreamResult(pdfStream, new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"));
        }
    }
}
