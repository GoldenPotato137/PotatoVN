using System.Collections.ObjectModel;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface ICategoryService
{
    /// <summary>
    /// 获取某个分类组，若没有则返回null
    /// </summary>
    public CategoryGroup? GetGroup(Guid id);
    
    /// <summary>
    /// 获取游玩状态分类组
    /// </summary>
    public CategoryGroup StatusGroup { get; }
    
    /// <summary>
    /// 开发商分类组
    /// </summary>
    public CategoryGroup DeveloperGroup { get; }

    /// <summary>
    /// 游戏引擎分类组
    /// </summary>
    public CategoryGroup EngineGroup { get; }
    
    public Task Init();

    public Task<ObservableCollection<CategoryGroup>> GetCategoryGroupsAsync();

    /// <summary>
    /// 新增自定义分类组。
    /// </summary>
    public CategoryGroup AddCategoryGroup(string name);

    /// <summary>
    /// 删除分类组及不再被其他分类组引用的分类。
    /// </summary>
    public void DeleteCategoryGroup(CategoryGroup categoryGroup);

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
    /// 向分类组添加分类
    /// </summary>
    /// <param name="categoryGroup">分类组</param>
    /// <param name="category">分类</param>
    public void AddCategoryToGroup(CategoryGroup categoryGroup, Category category);

    /// <summary>
    /// 从分类组移除分类
    /// </summary>
    /// <param name="categoryGroup">分类组</param>
    /// <param name="category">分类</param>
    public void RemoveCategoryFromGroup(CategoryGroup categoryGroup, Category category);

    /// <summary>
    /// 将源分类合并到目标分类，然后删除源分类 <br/>
    /// 如果目标分类和源分类相同，则不进行任何操作
    /// </summary>
    /// <param name="target">目标分类</param>
    /// <param name="source">源分类</param>
    public void Merge(Category target, Category source);
    
    /// <summary>
    /// 获取某个分类，若没有则返回null
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Category? GetCategory(Guid id);
    
    /// <summary>
    /// 获取某个分类，若没有则返回null
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    Category? GetCategory(string name);

    /// <summary>
    /// 获取某个游戏的开发商分类，若没有则返回null
    /// </summary>
    /// <param name="galgame"></param>
    /// <returns></returns>
    Category? GetDeveloperCategory(Galgame galgame);

    /// <summary>
    /// 获取某个游戏的游戏引擎分类，若没有则返回null
    /// </summary>
    /// <param name="galgame"></param>
    /// <returns></returns>
    Category? GetEngineCategory(Galgame galgame);

    /// <summary>
    /// 保存分类信息
    /// </summary>
    /// <param name="category">要保存的分类</param>
    /// <param name="categoryGroup">要保存的分类组</param>
    public void Save(Category? category = null, CategoryGroup? categoryGroup = null);
    
    /// <summary>
    /// 导出数据
    /// </summary>
    /// <returns></returns>
    Task ExportAsync(Action<string, int, int>? progress);
}
