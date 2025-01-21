namespace JsonFileConverterFunctionApp.src.Models
{
    internal class Declaration
    {
        public string NumeroTeledeclarant { get; set; } = string.Empty;
        public FormActivite FormActivite { get; set; } = new FormActivite();
        public List<FormFinancier> FormFinancierList { get; set; } = new List<FormFinancier>();
    }
}
