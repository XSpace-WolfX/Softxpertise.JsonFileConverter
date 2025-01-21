namespace JsonFileConverterFunctionApp.src.Models
{
    public class Formulaire
    {
        public string TypeAccueil { get; set; } = string.Empty;
        public List<DonneeTA> DonneeTAList { get; set; } = new List<DonneeTA>();
    }
}
