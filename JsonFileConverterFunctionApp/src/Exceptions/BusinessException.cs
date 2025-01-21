namespace JsonFileConverterFunctionApp.src.Exceptions
{
    // Exception personnalisée pour les erreurs métier
    public class BusinessException : Exception
    {
        public BusinessException(string message) : base(message) { }
        public BusinessException(string message, Exception innerException) : base(message, innerException) { }
    }
}