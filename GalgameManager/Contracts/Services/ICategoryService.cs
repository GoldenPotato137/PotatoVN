using System.Collections.ObjectModel;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface ICategoryService
{
    public Task Init();

    public Task<ObservableCollection<CategoryGroup>> GetCategoryGroupsAsync();

    /// <summary>
    /// 更新所有Galgame的分类
    /// </summary>
    public Task UpdateAllGames();
    
    /// <summary>
    /// 更新某个分类的信息（目前只有开发商的图片）
    /// </summary>
    /// <param name="category">分类</param>
    public void UpdateCategory(Category category);

    /// <summary>
    /// 删除分类
    /// </summary>
    /// <param name="category">分类</param>
    public void DeleteCategory(Category category);

    /// <summary>
    /// 将源分类合并到目标分类，然后删除源分类 <br/>
    /// 如果目标分类和源分类相同，则不进行任何操作
    /// </summary>
    /// <param name="target">目标分类</param>
    /// <param name="source">源分类</param>
    public void Merge(Category target, Category source);
}