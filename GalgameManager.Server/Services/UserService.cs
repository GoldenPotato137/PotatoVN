using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Models;
using Microsoft.IdentityModel.Tokens;

namespace GalgameManager.Server.Services;

public class UserService (IConfiguration config): IUserService
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
}