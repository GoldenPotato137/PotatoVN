using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IUserService
{
    public bool IsDefaultLoginEnable { get; }
    
    public string GetToken(User user);
    
    public long GetExpiryDateFromToken(string token);
    
    public Task UpdateLastModifiedAsync(int userId, long lastModifiedTimestamp);
}