using JsonFileConverterFunctionApp.src.Exceptions;
using JsonFileConverterFunctionApp.src.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace JsonFileConverterFunctionApp.src.Functions
{
    public class JsonFileConverterFunction
    {
        private readonly ILogger<JsonFileConverterFunction> _logger;
        private readonly IJsonFileConverterService _jsonFileConverterService;

        public JsonFileConverterFunction(ILogger<JsonFileConverterFunction> logger, IJsonFileConverterService jsonFileConverterService)
        {
            _logger = logger;
            _jsonFileConverterService = jsonFileConverterService;
        }

        [Function("JsonFileConverterFunction")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger JsonFileConverterFunction processed a request.");

            // Vérifie si le Content-Type est multipart/form-data
            if (!req.Headers.TryGetValues("Content-Type", out var contentTypeHeaders) ||
                !contentTypeHeaders.First().Contains("multipart/form-data"))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid content type. Expected multipart/form-data.");
                return badRequestResponse;
            }

            // Récupère le boundary du Content-Type
            var boundary = GetBoundaryFromContentType(contentTypeHeaders.First());
            if (boundary == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Boundary not found in Content-Type header.");
                return badRequestResponse;
            }

            MultipartReader multipartReader = new MultipartReader(boundary, req.Body);
            MultipartSection? section;

            Stream? fileStream = null;
            string? fileName = null;

            // Parcourt les sections multipart pour trouver un fichier
            while ((section = await multipartReader.ReadNextSectionAsync()) != null)
            {
                if (section.Headers != null && section.Headers.TryGetValue("Content-Disposition", out var contentDispositionValues))
                {
                    var contentDisposition = contentDispositionValues.ToString();

                    if (contentDisposition.Contains("form-data") && contentDisposition.Contains("filename"))
                    {
                        fileName = GetFileNameFromContentDisposition(contentDisposition);
                        fileStream = section.Body;
                        break;
                    }
                }
            }

            // Vérifie si un fichier est présent
            if (fileStream == null || string.IsNullOrEmpty(fileName))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("No file uploaded.");
                return badRequestResponse;
            }

            // 1. Vérifie si un fichier est présent
            //if (!req.Form.Files.Any()) return new BadRequestObjectResult("No file uploaded.");

            // 2. Vérifie si le fichier est null ou vide
            //var file = req.Form.Files[0];
            //if (file == null || file.Length == 0) return new BadRequestObjectResult("Invalid file.");

            // Vérifie l'extension du fichier
            var allowedExtensions = new[] { ".xls", ".xlsx" };
            var fileExtension = Path.GetExtension(fileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid file type. Only .xls and .xlsx are supported.");
                return badRequestResponse;
            }

            // 4. Vérifie la taille maximale
            using var memoryStream = new MemoryStream();
            if (memoryStream.Length > (5 * 1024 * 1024))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("File is too large. Maximum size allowed is 5 MB.");
                return badRequestResponse;
            }

            try
            {
                // Appelle le service
                var fileResult = _jsonFileConverterService.ExcelConverter(fileStream);

                // Sérialise le résultat en JSON
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var jsonString = JsonSerializer.Serialize(fileResult, jsonOptions);

                // Crée la réponse pour le fichier téléchargeable
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Content-Disposition", "attachment; filename=converted_file.json");
                await response.WriteStringAsync(jsonString); // Ajoute le contenu JSON en tant que fichier

                return response;
            }
            catch (BusinessException ex)
            {
                _logger.LogWarning($"Business error: {ex.Message}");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync(ex.Message);
                return badResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An unexpected error occurred.");
                return errorResponse;
            }
            //finally
            //{
            //    if (File.Exists(tempFilePath))
            //    {
            //        File.Delete(tempFilePath);
            //    }
            //}
        }

        private string? GetBoundaryFromContentType(string contentType)
        {
            var elements = contentType.Split(';');
            var boundaryElement = elements.FirstOrDefault(e => e.Trim().StartsWith("boundary="));
            return boundaryElement?.Split('=')[1].Trim();
        }

        private string? GetFileNameFromContentDisposition(string contentDisposition)
        {
            // Exemple : Content-Disposition: form-data; name="file"; filename="example.xlsx"
            var fileNamePart = contentDisposition.Split(';')
                .FirstOrDefault(part => part.Trim().StartsWith("filename="));

            return fileNamePart?.Split('=')[1]?.Trim('"');
        }
    }
}
