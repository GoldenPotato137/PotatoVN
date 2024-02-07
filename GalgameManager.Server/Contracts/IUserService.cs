using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IUserService
{
    public bool IsDefaultLoginEnable { get; }
    public string GetToken(User user);
}