using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Models;
using Microsoft.IdentityModel.Tokens;

namespace GalgameManager.Server.Services;

public class UserService (IConfiguration config, IUserRepository repository): IUserService
{
    private readonly string _jwtKey = config["AppSettings:JwtKey"]!;

    public bool IsDefaultLoginEnable { get; } = Convert.ToBoolean(config["AppSettings:DefaultLoginEnable"] ?? "True");

    public string GetToken(User user)
    {
        List<Claim> claims = new()
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Type.ToString()),
        };
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_jwtKey));
        SigningCredentials cred = new(key, SecurityAlgorithms.HmacSha512Signature);
        JwtSecurityToken token = new(claims: claims, expires: DateTime.Now.AddMonths(1), signingCredentials: cred);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    public long GetExpiryDateFromToken(string token)
    {
        JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token)) return 0;
        JwtSecurityToken? jwtToken = handler.ReadJwtToken(token);
        Claim? expClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);
        if (expClaim != null && long.TryParse(expClaim.Value, out var expValue))
            return expValue;
        return 0;
    }

    public async Task UpdateLastModifiedAsync(int userId, long lastModifiedTimestamp)
    {
        User? user = await repository.GetUserAsync(userId);
        if (user is null) return;
        if (lastModifiedTimestamp > user.LastGalChangedTimeStamp)
        {
            user.LastGalChangedTimeStamp = lastModifiedTimestamp;
            await repository.UpdateUserAsync(user);
        }
    }
}