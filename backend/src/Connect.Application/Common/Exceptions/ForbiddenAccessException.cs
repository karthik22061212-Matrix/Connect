namespace Connect.Application.Common.Exceptions;

public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() : base("Access to this resource is forbidden.")
    {
    }

    public ForbiddenAccessException(string message) : base(message)
    {
    }
}
