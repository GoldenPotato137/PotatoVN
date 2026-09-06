using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.WinApp.Base.Contracts;

namespace GalgameManager.Services;

public partial class PluginService
{
    public partial class PotatoVnApiHost : IPotatoVnApi
    {
        private readonly ICategoryService _categoryService = App.GetService<ICategoryService>();

        public async Task<List<CategoryGroup>> GetCategoryGroupsAsync() =>
            (await _categoryService.GetCategoryGroupsAsync()).ToList();

        public CategoryGroup? GetCategoryGroup(Guid id) => _categoryService.GetGroup(id);

        public Category? GetCategory(Guid id) => _categoryService.GetCategory(id);

        public Category? GetCategory(string name) => _categoryService.GetCategory(name);

        public Category? GetDeveloperCategory(Galgame game) =>
            _categoryService.GetDeveloperCategory(ResolveGame(game));

        public Category? GetEngineCategory(Galgame game) =>
            _categoryService.GetEngineCategory(ResolveGame(game));

        public CategoryGroup StatusCategoryGroup => _categoryService.StatusGroup;

        public CategoryGroup DeveloperCategoryGroup => _categoryService.DeveloperGroup;

        public CategoryGroup EngineCategoryGroup => _categoryService.EngineGroup;

        public CategoryGroup AddCategoryGroup(string name) => _categoryService.AddCategoryGroup(name);

        public void DeleteCategoryGroup(CategoryGroup group) => _categoryService.DeleteCategoryGroup(group);

        public void SaveCategory(Category category) => _categoryService.Save(category);

        public void SaveCategoryGroup(CategoryGroup group) => _categoryService.Save(categoryGroup: group);

        public void DeleteCategory(Category category) => _categoryService.DeleteCategory(category);

        public void AddCategoryToGroup(CategoryGroup group, Category category) =>
            _categoryService.AddCategoryToGroup(group, category);

        public void RemoveCategoryFromGroup(CategoryGroup group, Category category) =>
            _categoryService.RemoveCategoryFromGroup(group, category);

        public void MergeCategories(Category target, Category source) => _categoryService.Merge(target, source);

        public Task RefreshGameCategoriesAsync() => _categoryService.UpdateAllGames();

        public void RefreshCategory(Category category) => _categoryService.UpdateCategory(category);
    }
}
