using GalgameManager.Server.Models;

namespace GalgameManager.Server.Contracts;

public interface IUserRepository
{
    public Task<Result<User>> GetUserAsync(int id);
    
    public Task<Result<User>> GetUserAsync(string username);

    public Task<Result<User>> RegisterAsync(UserRegisterDto payload);
}