using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface ICategoryService
{
    public Task Init();

    /// <summary>
    /// 更新所有Galgame的分类
    /// </summary>
    public Task UpdateAllGames();
}