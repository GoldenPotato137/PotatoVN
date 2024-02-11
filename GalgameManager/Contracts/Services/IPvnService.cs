using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Services;

namespace GalgameManager.Contracts.Services;

public interface IPvnService
{
    public Uri BaseUri { get; }

    public Action<PvnServiceStatus>? StatusChanged { get; set; }

    public Task<PvnServerInfo?> GetServerInfoAsync();

    public Task<PvnAccount?> LoginAsync(string username, string password);

    public Task<PvnAccount?> RegisterAsync(string username, string password);

    public Task<PvnAccount?> LoginViaBangumiAsync();

    public Task<PvnAccount?> ModifyAccountAsync(string? userDisplayName = null, string? avatarPath = null,
        string? newPassword = null, string? oldPassword = null);

    public Task LogOutAsync();
}