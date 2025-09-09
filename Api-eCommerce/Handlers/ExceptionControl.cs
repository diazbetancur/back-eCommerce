namespace Api_eCommerce.Handlers
{
    public class ExceptionControl
    {
        private readonly Dictionary<string, Type> registeredExceptions;

        public ExceptionControl()
        {
            registeredExceptions = new Dictionary<string, Type>
        {
            { typeof(SystemException).Name, typeof(SystemException)},
            { typeof(HttpRequestException).Name, typeof(HttpRequestException)}
        };
        }

        public string GetExceptionMessage(Exception ex)
        {
            string message = string.Empty;
            if (ex != null)
            {
                message = ex.Message;
                registeredExceptions.TryGetValue(ex.GetType().Name, out Type type);
                if (type == null)
                {
                    message = "An application error has occurred.";
                }
            }
            return message;
        }
    }
}