namespace CC.Domain.Helpers
{
    public class ActionResponse<T>
    {
        public bool WasSuccessful { get; set; }
        public T Result { get; set; }
        public string Message { get; set; }
    }
}