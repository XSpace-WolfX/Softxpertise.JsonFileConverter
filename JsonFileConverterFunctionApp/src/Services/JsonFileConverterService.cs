using ExcelDataReader;
using JsonFileConverterFunctionApp.src.Exceptions;
using JsonFileConverterFunctionApp.src.Models;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JsonFileConverterFunctionApp.src.Services
{
    public class JsonFileConverterService : IJsonFileConverterService
    {
        private readonly ILogger<JsonFileConverterService> _logger;

        public JsonFileConverterService(ILogger<JsonFileConverterService> logger)
        {
            _logger = logger;
        }

        public JsonResult ExcelConverter(Stream? fileStream)
        {
            _logger.LogInformation("Starting service ExcelConverter...");

            try
            {
                Stream readableStream;
                MemoryStream memoryStream = new MemoryStream();
                if (fileStream != null)
                    fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0; // Réinitialise la position au début
                readableStream = memoryStream; // Utilise le MemoryStream

                //using (var stream = File.OpenRead(filePath))
                using (var reader = ExcelReaderFactory.CreateReader(readableStream))
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

                    JsonResult jsonResult = new JsonResult();

                    jsonResult.Exercice = DateTime.Now.Year;
                    jsonResult.TypeDeclaration = "REEL";
                    jsonResult.MoisActualisation = DateTime.Now.Month;

                    Dictionary<string, List<string>> MappingCAF = LoadXmlMapping(Path.Combine(Directory.GetCurrentDirectory(), "src", "Resources", "Mapping_CAF.xml"));
                    string? numeroTeledeclarant; 

                    foreach (DataTable dataTable in dataSet.Tables)
                    {
                        if (dataTable.TableName.Contains("EAJE"))
                            jsonResult.TypeAide = "EAJE";

                        numeroTeledeclarant = FindNumeroTeledeclarant(dataTable.TableName);
                        if (numeroTeledeclarant == null)
                        {
                            throw new BusinessException("No valid télédéclarant found.");
                        }

                        if (!dataTable.TableName.ToLower().Contains("act") && !dataTable.TableName.ToLower().Contains("fin"))
                        {
                            throw new BusinessException("Sheet does not contain identifiable data type (act/fin).");
                        }

                        // Récupérer ou créer une déclaration dans JsonResult.DeclarationList
                        var declaration = jsonResult.DeclarationList
                            .FirstOrDefault(d => d.NumeroTeledeclarant == numeroTeledeclarant);

                        if (declaration == null)
                        {
                            declaration = new Declaration
                            {
                                NumeroTeledeclarant = numeroTeledeclarant
                            };

                            jsonResult.DeclarationList.Add(declaration);
                        }

                        if (dataTable.TableName.ToLower().Contains("act"))
                        {
                            Formulaire form = new Formulaire();

                            form.TypeAccueil = "COL";

                            // Parcourir les cellules du tableau
                            for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
                            {
                                for (int colIndex = 0; colIndex < dataTable.Columns.Count; colIndex++)
                                {
                                    var cellValue = dataTable.Rows[rowIndex][colIndex]?.ToString();
                                    if (string.IsNullOrEmpty(cellValue)) continue;

                                    // Identifier le code technique
                                    var codeTechnique = FindCodeTechnique(cellValue, MappingCAF);
                                    if (codeTechnique == null) continue;

                                    // Trouver la valeur associée
                                    var associatedValue = GetAssociatedValue(dataTable, rowIndex, colIndex);
                                    if (associatedValue == null) continue;

                                    if (associatedValue.ToLower().Trim() == "oui")
                                        form.DonneeTAList.Add(new DonneeTA { CodeDonnee = codeTechnique, Valeur = true });
                                    else if (associatedValue.ToLower().Trim() == "non")
                                        form.DonneeTAList.Add(new DonneeTA { CodeDonnee = codeTechnique, Valeur = false });
                                    else
                                        form.DonneeTAList.Add(new DonneeTA { CodeDonnee = codeTechnique, Valeur = int.Parse(associatedValue) });
                                }
                            }

                            declaration.FormActivite.Formulaire.Add(form);
                        }
                        else if (dataTable.TableName.ToLower().Contains("fin"))
                        { 
                            // Parcourir les cellules du tableau
                            for (int rowIndex = 0; rowIndex < dataTable.Rows.Count; rowIndex++)
                            {
                                for (int colIndex = 0; colIndex < dataTable.Columns.Count; colIndex++)
                                {
                                    var cellValue = dataTable.Rows[rowIndex][colIndex]?.ToString();
                                    if (string.IsNullOrEmpty(cellValue)) continue;

                                    // Identifier le code technique
                                    var codeTechnique = FindCodeTechnique(cellValue, MappingCAF);
                                    if (codeTechnique == null) continue;

                                    // Trouver la valeur associée
                                    var associatedValue = GetAssociatedValue(dataTable, rowIndex, colIndex);
                                    if (associatedValue == null) continue;

                                    declaration.FormFinancierList.Add(new FormFinancier { Compte = codeTechnique, Valeur = decimal.Parse(associatedValue) });
                                }
                            }
                        }
                    }

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

        public Dictionary<string, List<string>> LoadXmlMapping(string xmlFilePath)
        {
            var mapping = new Dictionary<string, List<string>>();

            // Charger le fichier XML
            var xmlDocument = XDocument.Load(xmlFilePath);

            // Parcourir les éléments <Entry> dans le fichier XML
            foreach (var entry in xmlDocument.Descendants("Entry"))
            {
                // Extraire le CodeTechnique
                var codeTechnique = entry.Element("CodeTechnique")?.Value;

                // Extraire les mots-clés (MotsCles -> <Mot>)
                var motsCles = entry.Element("MotsCles")?
                    .Elements("Mot")
                    .Select(m => m.Value.Trim())
                    .ToList();

                if (!string.IsNullOrEmpty(codeTechnique) && motsCles != null)
                {
                    mapping[codeTechnique] = motsCles;
                }
            }

            return mapping;
        }

        private string? FindCodeTechnique(string input, Dictionary<string, List<string>> mapping)
        {
            // Normaliser uniquement la chaîne d'entrée
            input = RemoveDiacritics(input.ToLower());
            var matchedCodes = new List<(string Code, int MatchCount)>();

            foreach (var entry in mapping)
            {
                var codeTechnique = entry.Key;
                var keywords = entry.Value;

                // Vérifier si tous les mots-clés de ce code technique sont présents dans l'entrée
                if (keywords.All(keyword => input.Contains(keyword)))
                {
                    // Ajouter la correspondance avec le nombre total de mots-clés
                    matchedCodes.Add((codeTechnique, keywords.Count));
                }
            }

            // Si une seule correspondance est trouvée, la retourner
            if (matchedCodes.Count == 1)
            {
                return matchedCodes.First().Code;
            }

            // Si plusieurs correspondances existent, gérer l'ambiguïté
            if (matchedCodes.Count > 1)
            {
                // Trouver le maximum de mots-clés correspondants
                var maxMatchCount = matchedCodes.Max(m => m.MatchCount);

                // Filtrer les correspondances ayant ce maximum
                var bestMatches = matchedCodes.Where(m => m.MatchCount == maxMatchCount).ToList();

                if (bestMatches.Count > 1)
                {
                    // Lever une exception pour signaler l'ambiguïté
                    throw new InvalidOperationException($"Ambiguity detected: Multiple codes match equally: {string.Join(", ", bestMatches.Select(m => m.Code))}");
                }

                // Si une seule correspondance reste après le filtre, la retourner
                return bestMatches.First().Code;
            }

            return null; // Aucune correspondance trouvée
        }

        private string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private string? GetAssociatedValue(DataTable dataTable, int rowIndex, int colIndex)
        {
            if (dataTable.TableName.ToLower().Contains("act"))
            {
                // Essayer la cellule à droite
                if (colIndex + 1 < dataTable.Columns.Count)
                {
                    var rightValue = dataTable.Rows[rowIndex][colIndex + 1]?.ToString();
                    if (IsValidActivityValue(rightValue)) return rightValue;
                }

                // Essayer la cellule en dessous
                if (rowIndex + 1 < dataTable.Rows.Count)
                {
                    var belowValue = dataTable.Rows[rowIndex + 1][colIndex]?.ToString();
                    if (IsValidActivityValue(belowValue)) return belowValue;
                }
            }
            else if (dataTable.TableName.ToLower().Contains("fin"))
            {
                // Essayer la cellule à droite
                if (colIndex + 1 < dataTable.Columns.Count)
                {
                    var rawValue = dataTable.Rows[rowIndex][colIndex + 1]?.ToString();
                    var rightValue = CleanNumericValue(rawValue);
                    if (IsValidFinancierValue(rightValue)) return rightValue;
                }

                // Essayer la cellule en dessous
                if (rowIndex + 1 < dataTable.Rows.Count)
                {
                    var rawValue = dataTable.Rows[rowIndex + 1][colIndex]?.ToString();
                    var belowValue = CleanNumericValue(rawValue);
                    if (IsValidFinancierValue(belowValue)) return belowValue;
                }
            }

            // Aucun résultat trouvé
            return null;
        }

        // Valider si une valeur est soit numérique, soit booléenne
        private bool IsValidActivityValue(string? value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            // Vérifier si c'est un nombre
            if (int.TryParse(value, out _)) return true;

            // Vérifier si c'est "oui" ou "non"
            var lowerValue = value.ToLower().Trim();
            if (lowerValue == "oui" || lowerValue == "non") return true;

            // Pas une valeur valide
            return false;
        }

        // Valider si une valeur est soit decimal
        private bool IsValidFinancierValue(string? value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            // Vérifier si c'est un nombre
            if (decimal.TryParse(value, out _)) return true;

            // Pas une valeur valide
            return false;
        }

        public string? FindNumeroTeledeclarant(string input)
        {
            Regex regex = new Regex(@"\b\d{3}-\d{4}-\d{4}\b");
            var match = regex.Match(input);

            return match.Success ? match.Value : null;
        }

        private string CleanNumericValue(string? input)
        {
            if (string.IsNullOrEmpty(input)) return "0";

            // Supprimer les espaces, les symboles monétaires et tout ce qui n'est pas un chiffre, une virgule ou un point
            var cleaned = input.Replace("€", "").Replace(" ", "").Trim();

            // Remplacer la virgule par un point si nécessaire (pour respecter le format invariant de decimal)
            cleaned = cleaned.Replace(",", ".");

            return cleaned;
        }
    }
}
