namespace MailMonitor.Application.Exceptions
{
    public sealed record ValidationError(string PropertyName, string ErrorMessage);

    public sealed class ValidationException : Exception
    {
        public ValidationException(IReadOnlyCollection<ValidationError> errors)
            : base("One or more validation errors occurred.")
        {
            Errors = errors;
        }

        public IReadOnlyCollection<ValidationError> Errors { get; }
    }
}
