using FluentValidation.Results;

namespace Cart.Application.Exceptions
{
    public class ValidationException : ApplicationException
    {
        public ValidationException() : base("One or more validation errors occurred")
        {
            Errors = new List<KeyValuePair<string, string>>();
        }

        public ValidationException(IEnumerable<ValidationFailure> failures) : this()
        {
            Errors = failures.Select(f => new KeyValuePair<string, string>(f.PropertyName, f.ErrorMessage));
        }

        public ValidationException(IEnumerable<KeyValuePair<string, string>> errorList) : this()
        {
            Errors = errorList;
        }

        public IEnumerable<KeyValuePair<string, string>> Errors { get; }

    }
}
