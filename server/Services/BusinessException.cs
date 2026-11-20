namespace DataDiode;

/// <summary>A refused request with a readable message. The middleware turns it into JSON { message } with this status code.</summary>
public class BusinessException : Exception
{
    public int StatusCode { get; }

    public BusinessException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}
