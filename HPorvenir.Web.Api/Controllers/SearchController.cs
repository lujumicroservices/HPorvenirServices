using HPorvenir.Document;

using HPorvenir.Elastic;

using HPorvenir.Storage;

using HPorvenir.Web.Api.Model;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.Logging;

using System;

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

        private readonly ILogger<SearchController> _logger;

        private readonly ElasticOptions _elasticOptions;

        private readonly string _searchIndex;



        public SearchController(IStorage storageProvider, ILogger<SearchController> logger, IConfiguration configuration)

        {

            _storageProvider = storageProvider;

            _logger = logger;

            _elasticOptions = IndexerSettingsLoader.Load().Elastic;

            _searchIndex = configuration["Elastic:SearchAlias"] ?? _elasticOptions.SearchAlias;

        }



        [HttpPost("simple")]

        public IActionResult Search([FromBody] SearchRequest searchRequest)

        {

            var validation = ValidateSearchRequest(searchRequest, requireFileName: false);

            if (validation != null)

            {

                return validation;

            }



            _logger.LogInformation(

                "Search/simple started. User={User}, Terms={Terms}, IsPhrase={IsPhrase}, StartDate={StartDate}, EndDate={EndDate}",

                User?.Identity?.Name,

                string.Join("|", searchRequest.Terms ?? Array.Empty<string>()),

                searchRequest.IsPhrase,

                searchRequest.StartDate,

                searchRequest.EndDate);



            try

            {

                var searcher = new Searcher(_elasticOptions, _searchIndex);

                var result = searcher.Search(searchRequest.Terms, searchRequest.IsPhrase, searchRequest.StartDate, searchRequest.EndDate);



                _logger.LogInformation("Search/simple completed. User={User}, ResultCount={ResultCount}", User?.Identity?.Name, result?.Count ?? 0);

                return Ok(result);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Search/simple failed. User={User}, Request={@SearchRequest}", User?.Identity?.Name, searchRequest);

                return StatusCode(500, new { error = "Error al ejecutar la búsqueda", code = 2001 });

            }

        }



        [HttpPost("file")]

        public async Task<ActionResult> FileInfoAsync([FromBody] SearchRequest searchRequest)

        {

            var validation = ValidateSearchRequest(searchRequest, requireFileName: true);

            if (validation != null)

            {

                return validation;

            }



            _logger.LogInformation(

                "Search/file started. User={User}, FileName={FileName}, Terms={Terms}, IsPhrase={IsPhrase}, StartDate={StartDate}, EndDate={EndDate}",

                User?.Identity?.Name,

                searchRequest.FileName,

                string.Join("|", searchRequest.Terms),

                searchRequest.IsPhrase,

                searchRequest.StartDate,

                searchRequest.EndDate);



            try

            {

                bool isPDF = searchRequest.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

                var searcher = new Searcher(_elasticOptions, _searchIndex);



                var resultHits = searcher.FileDetails(

                    searchRequest.FileName,

                    searchRequest.Terms,

                    searchRequest.IsPhrase,

                    searchRequest.StartDate,

                    searchRequest.EndDate);



                _logger.LogInformation(

                    "Search/file elasticsearch completed. FileName={FileName}, HitCount={HitCount}",

                    searchRequest.FileName,

                    resultHits?.Count ?? 0);



                Stream fileStream;

                try

                {

                    fileStream = await _storageProvider.ReadPathFromIndexAsync(searchRequest.FileName);

                    _logger.LogInformation(

                        "Search/file blob loaded. FileName={FileName}, StreamLength={StreamLength}",

                        searchRequest.FileName,

                        fileStream?.Length);

                }

                catch (Exception ex)

                {

                    _logger.LogError(ex, "Search/file blob not found. FileName={FileName}", searchRequest.FileName);

                    return StatusCode(500, new { error = "Archivo no encontrado, el problema fue reportado automaticamente al administrador", code = 1000 });

                }



                var doc = new PDFDocument();

                var pdfStream = doc.ProcessFile(fileStream, resultHits, searchRequest.Terms, isPDF, removeWhaterMark: false);



                _logger.LogInformation(

                    "Search/file completed. User={User}, FileName={FileName}, PdfLength={PdfLength}",

                    User?.Identity?.Name,

                    searchRequest.FileName,

                    pdfStream?.Length);



                return new FileStreamResult(pdfStream, new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/pdf"));

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Search/file failed. User={User}, Request={@SearchRequest}", User?.Identity?.Name, searchRequest);

                return StatusCode(500, new { error = "Error al generar el documento de búsqueda", code = 2002 });

            }

        }



        private ActionResult ValidateSearchRequest(SearchRequest searchRequest, bool requireFileName)

        {

            if (!ModelState.IsValid)

            {

                var errors = ModelState

                    .Where(x => x.Value.Errors.Count > 0)

                    .ToDictionary(

                        x => x.Key,

                        x => x.Value.Errors.Select(e => e.ErrorMessage).ToArray());



                _logger.LogWarning(

                    "Search request model binding failed. User={User}, Errors={@Errors}, Request={@SearchRequest}",

                    User?.Identity?.Name,

                    errors,

                    searchRequest);



                return BadRequest(new { error = "Solicitud inválida", code = 4000, details = errors });

            }



            if (searchRequest == null)

            {

                _logger.LogWarning("Search request body is null. User={User}", User?.Identity?.Name);

                return BadRequest(new { error = "El cuerpo de la solicitud es requerido", code = 4001 });

            }



            if (searchRequest.Terms == null || searchRequest.Terms.Length == 0)

            {

                _logger.LogWarning("Search request missing terms. User={User}, Request={@SearchRequest}", User?.Identity?.Name, searchRequest);

                return BadRequest(new { error = "Terms es requerido y no puede estar vacío", code = 4002, received = searchRequest });

            }



            if (requireFileName && string.IsNullOrWhiteSpace(searchRequest.FileName))

            {

                _logger.LogWarning("Search/file missing FileName. User={User}, Request={@SearchRequest}", User?.Identity?.Name, searchRequest);

                return BadRequest(new { error = "FileName es requerido para search/file", code = 4003, received = searchRequest });

            }



            if (searchRequest.StartDate.HasValue != searchRequest.EndDate.HasValue)

            {

                _logger.LogWarning(

                    "Search request has partial date range. User={User}, StartDate={StartDate}, EndDate={EndDate}",

                    User?.Identity?.Name,

                    searchRequest.StartDate,

                    searchRequest.EndDate);



                return BadRequest(new { error = "StartDate y EndDate deben enviarse juntos o omitirse", code = 4004, received = searchRequest });

            }



            return null;

        }

    }

}


