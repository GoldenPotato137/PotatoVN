using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;

namespace GalgameManager.Contracts.Services;

public interface IPvnService
{
    public Uri BaseUri { get; }

    public Action<PvnServiceStatus>? StatusChanged { get; set; }
    
    public PvnSyncTask? SyncTask { get; }

    public void Startup();

    public Task<PvnServerInfo?> GetServerInfoAsync();

    public Task<PvnAccount?> LoginAsync(string username, string password);

    public Task<PvnAccount?> RegisterAsync(string username, string password);

    public Task<PvnAccount?> LoginViaBangumiAsync();

    public Task<PvnAccount?> ModifyAccountAsync(string? userDisplayName = null, string? avatarPath = null,
        string? newPassword = null, string? oldPassword = null);
    
    public Task<long> GetLastGalChangedTimeStampAsync();
    
    public Task<List<GalgameDto>> GetChangedGalgamesAsync();
    
    public Task<List<int>> GetDeletedGalgamesAsync();

    public void SyncGames();
    
    public void Upload(Galgame galgame, PvnUploadProperties properties);

    /// <summary>
    /// 不要直接调用，上传游戏数据请使用<see cref="Upload"/>
    /// </summary>
    /// <param name="galgame"></param>
    /// <returns>potatoVN id</returns>
    public Task<int> UploadInternal(Galgame galgame);
    
    /// <summary>
    /// 不要调用它，这个函数只有PvnSyncTask才能调用
    /// </summary>
    public Task DeleteInternal(int pvnId);

    public Task LogOutAsync();
}