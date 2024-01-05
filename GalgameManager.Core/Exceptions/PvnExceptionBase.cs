namespace GalgameManager.Core.Exceptions;

public class PvnExceptionBase : Exception
{
    public PvnExceptionBase(string message) : base(message) { }

    public PvnExceptionBase(string message, Exception innerException) : base(message, innerException) { }
}