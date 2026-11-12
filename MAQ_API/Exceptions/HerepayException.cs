namespace MAQ_API.Exceptions;

/// <summary>Herepay rejected or failed a request. <see cref="IsClientError"/> = 422/400 (caller's data), else 401/network (our side).</summary>
public class HerepayException(string message, bool isClientError) : Exception(message)
{
    public bool IsClientError { get; } = isClientError;
}
