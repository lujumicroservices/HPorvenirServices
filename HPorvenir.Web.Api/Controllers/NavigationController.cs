using HPorvenir.Document;
using HPorvenir.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
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
        public async Task<IActionResult> GetFileAsync(string pathId)
        {

            Stream fileStream = null;
            try
            {
                fileStream = await _storageProvider.ReadPathAsync(WebUtility.UrlDecode(pathId));
            }
            catch (Exception ex)
            {

                return StatusCode(500, new { error = "Archivo no encontrado, el problema fue reportado automaticamente al administrador", code = 1000 });
            }

            PDFDocument doc = new PDFDocument();
            var isAdmin = HttpContext.User.Claims.Any(x => x.Type == ClaimTypes.Role && x.Value == "admin");
            var pdfStream = doc.ProcessFile(fileStream, null, isAdmin);
            return new FileStreamResult(pdfStream, new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"));            
        }




    }
}
