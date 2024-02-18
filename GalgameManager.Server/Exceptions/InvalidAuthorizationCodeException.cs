namespace GalgameManager.Server.Exceptions;

public class InvalidAuthorizationCodeException(string msg) : Exception(msg);