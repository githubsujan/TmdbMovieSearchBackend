namespace MovieSearchCase.Domain.Exceptions;

public class TvException : ProblemDetailsException
{
    public const string ExceptionTitle = "Tv request failed";

    public TvException(string title, ErrorType errorType, string details, Uri? instance = null)
        : base(title, errorType, details, instance)
    {
    }
}
