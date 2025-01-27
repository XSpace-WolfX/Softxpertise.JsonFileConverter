namespace JsonFileConverterFunctionApp.src.Models
{
    public class JsonResult
    {
        public string TypeAide { get; set; } = string.Empty;
        public int Exercice { get; set; }
        public string TypeDeclaration { get; set; } = string.Empty;
        public int MoisActualisation { get; set; }
        public List<Declaration> DeclarationList { get; set; } = new List<Declaration>();
    }
}
