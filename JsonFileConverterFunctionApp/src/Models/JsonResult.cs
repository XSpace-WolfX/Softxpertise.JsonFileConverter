namespace JsonFileConverterFunctionApp.src.Models
{
    internal class JsonResult
    {
        public string TypeAide { get; set; } = string.Empty;
        public int Exercice { get; set; }
        public string TypeDeclaration { get; set; } = string.Empty;
        public string MoisActualisation { get; set; } = string.Empty;
        public List<Declaration> DeclarationList { get; set; } = new List<Declaration>();
    }
}
