namespace MailMonitor.Domain.Abstractions
{
    public record Error(string Code, string Name)
    {
        public static readonly Error None = new(string.Empty, string.Empty);

        public static readonly Error NullValue = new("Error.NullValue", "Null value was provided");

        public static readonly Error EmptyValue = new("Error.EmptyValue", "A null or empty value was provided.");
        public static readonly Error Failure = new("Error.Failure", "A general failure occurred while processing the request.");
    }
}
