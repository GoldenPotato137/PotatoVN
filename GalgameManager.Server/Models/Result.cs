using GalgameManager.Server.Enums;

namespace GalgameManager.Server.Models;

public class Result<T>(ResultType type, T? item = default)
{
    public T? Item = item;
    public ResultType Type = type;
}

public class OkResult<T>(T? item = default) : Result<T>(ResultType.Ok, item);