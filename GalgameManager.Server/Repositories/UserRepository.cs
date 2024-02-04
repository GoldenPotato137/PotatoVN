using GalgameManager.Server.Contracts;
using GalgameManager.Server.Data;
using GalgameManager.Server.Enums;
using GalgameManager.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalgameManager.Server.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DataContext _context;
    private readonly IConfiguration _config;

    public UserRepository(DataContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    public async Task<Result<User>> GetUserAsync(int id)
    {
        User? user = await _context.User.FindAsync(id);
        return new Result<User>(user is not null ? ResultType.Ok : ResultType.NotFound, user);
    }

    public async Task<Result<User>> GetUserAsync(string username)
    {
        User? user = await _context.User.FirstOrDefaultAsync(u => u.UserName == username);
        return new Result<User>(user is not null ? ResultType.Ok : ResultType.NotFound, user); 
    }

    public async Task<Result<User>> RegisterAsync(UserRegisterDto payload)
    {
        if ((await GetUserAsync(payload.UserName)).Type != ResultType.NotFound)
            return new Result<User>(ResultType.BadRequest);
        return null;
    }
}