namespace JsonFileConverterFunctionApp.src.Models
{
    internal class Formulaire
    {
        public string TypeAccueil { get; set; } = string.Empty;
        public List<DonneeTA> DonneeTAList { get; set; } = new List<DonneeTA>();
    }
}
