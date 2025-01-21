using JsonFileConverterFunctionApp.src.Exceptions;
using JsonFileConverterFunctionApp.src.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger JsonFileConverterFunction processed a request.");

            // 1. Vérifie si un fichier est présent
            if (!req.Form.Files.Any()) return new BadRequestObjectResult("No file uploaded.");

            // 2. Vérifie si le fichier est null ou vide
            var file = req.Form.Files[0];
            if (file == null || file.Length == 0) return new BadRequestObjectResult("Invalid file.");

            // 3. Vérifie l'extension du fichier
            var allowedExtensions = new[] { ".xls", ".xlsx" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return new BadRequestObjectResult("Invalid file type. Only .xls and .xlsx are supported.");
            }

            // 4. Vérifie la taille maximale
            if (file.Length > 5 * 1024 * 1024) return new BadRequestObjectResult("File is too large. Maximum size allowed is 5 MB.");

            string tempFilePath = "";

            try
            {
                // Appelle le service
                var fileResult = _jsonFileConverterService.ExcelConverter(file);

                // Crée le fichier JSON temporairement
                tempFilePath = Path.GetTempFileName(); // Chemin temporaire
                var fileName = "converted_file.json";

                // Sérialise le JSON avec System.Text.Json
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true // Formatage lisible pour les humains
                };

                var jsonString = JsonSerializer.Serialize(fileResult, jsonOptions);
                await File.WriteAllTextAsync(tempFilePath, jsonString);

                // Retourne le fichier pour téléchargement
                var fileBytes = await File.ReadAllBytesAsync(tempFilePath);
                var result = new FileContentResult(fileBytes, "application/json")
                {
                    FileDownloadName = fileName
                };

                // Supprime le fichier temporaire
                File.Delete(tempFilePath);

                return new OkObjectResult(result);
            }
            catch (BusinessException ex)
            {
                _logger.LogWarning($"Business error: {ex.Message}");
                return new BadRequestObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error: {ex.Message}");
                return new ObjectResult("An unexpected error occurred.") { StatusCode = 500 };
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
    }
}
