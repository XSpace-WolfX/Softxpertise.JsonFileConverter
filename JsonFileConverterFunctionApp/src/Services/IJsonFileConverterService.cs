using Microsoft.AspNetCore.Http;
using JsonFileConverterFunctionApp.src.Models;

namespace JsonFileConverterFunctionApp.src.Services
{
    public interface IJsonFileConverterService
    {
        JsonResult ExcelConverter(Stream? fileStream);
    }
}
