namespace GalgameManager.Server.Exceptions;

/// <summary>用户名或绑定的bgm账户与已有用户重复</summary>
public class DuplicateUserException(string msg) : Exception(msg);