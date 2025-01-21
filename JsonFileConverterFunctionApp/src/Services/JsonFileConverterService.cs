using ExcelDataReader;
using JsonFileConverterFunctionApp.src.Exceptions;
using JsonFileConverterFunctionApp.src.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Data;

namespace JsonFileConverterFunctionApp.src.Services
{
    public class JsonFileConverterService : IJsonFileConverterService
    {
        private readonly ILogger<JsonFileConverterService> _logger;

        public JsonFileConverterService(ILogger<JsonFileConverterService> logger)
        {
            _logger = logger;
        }

        public JsonResult ExcelConverter(IFormFile file)
        {
            _logger.LogInformation("Starting service ExcelConverter...");

            try
            {
                using (var stream = file.OpenReadStream())
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = _ => new ExcelDataTableConfiguration
                        {
                            UseHeaderRow = true
                        }
                    });

                    if (dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count == 0)
                    {
                        throw new BusinessException("The uploaded Excel file is empty or contains no data.");
                    }

                    var dataTable = dataSet.Tables[0];

                    var jsonResult = new JsonResult
                    {
                        TypeAide = dataTable.Rows[0]["typeAide"].ToString() ?? string.Empty,
                        Exercice = Convert.ToInt32(dataTable.Rows[0]["exercice"]),
                        TypeDeclaration = dataTable.Rows[0]["typeDeclaration"].ToString() ?? string.Empty,
                        MoisActualisation = dataTable.Rows[0]["moisActualisation"].ToString() ?? string.Empty,
                        DeclarationList = dataTable.AsEnumerable().GroupBy(row => row["numeroTeledeclarant"].ToString())
                            .Select(group => new Declaration
                            {
                                NumeroTeledeclarant = group.Key ?? string.Empty,
                                FormActivite = new FormActivite
                                {
                                    Formulaire = group.Select(row => new Formulaire
                                    {
                                        TypeAccueil = row["typeAccueil"].ToString() ?? string.Empty,
                                        DonneeTAList = group.Select(donneeRow => new DonneeTA
                                        {
                                            CodeDonnee = donneeRow["codeDonnee"].ToString() ?? string.Empty,
                                            Valeur = Convert.ToDecimal(donneeRow["valeur"])
                                        }).ToList()
                                    }).ToList()
                                },
                                FormFinancierList = group.Select(financierRow => new FormFinancier
                                {
                                    Compte = financierRow["compte"].ToString() ?? string.Empty,
                                    Valeur = Convert.ToDecimal(financierRow["valeurFinanciere"])
                                }).ToList()
                            }).ToList()
                    };

                    return jsonResult;
                }
            }
            catch (FormatException ex)
            {
                throw new BusinessException("The Excel file contains invalid data format.", ex);
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new BusinessException("The Excel file does not contain the expected columns or structure.", ex);
            }
            catch (IOException ex)
            {
                throw new BusinessException("An error occurred while reading the Excel file.", ex);
            }
            catch (Exception ex)
            {
                throw new BusinessException("An unexpected error occurred during the file processing.", ex);
            }
        }
    }
}
