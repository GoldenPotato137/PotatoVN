using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;

namespace GalgameManager.Services;

public class CategoryService : ICategoryService
{
    private ObservableCollection<CategoryGroup> _categoryGroups = new();
    private readonly GalgameCollectionService _galgameService;
    private CategoryGroup? _developerGroup, _statusGroup;
    private bool _isInit;
    private readonly ILocalSettingsService _localSettings;

    public CategoryService(ILocalSettingsService localSettings, IDataCollectionService<Galgame> galgameService)
    {
        _localSettings = localSettings;
        _galgameService = (galgameService as GalgameCollectionService)!;
        _galgameService.PhrasedEvent2 += UpdateCategory;
        App.MainWindow.AppWindow.Closing += async (_, _) => await SaveAsync();
    }

    private static void SetBelong(Galgame galgame, Category category)
    {
        category.Add(galgame);
        galgame.Categories.Add(category);
    }

    public async Task Init()
    {
        if (_isInit) return;
        _categoryGroups = await _localSettings.ReadSettingAsync<ObservableCollection<CategoryGroup>>
            (KeyValues.CategoryGroups, true) ?? new ObservableCollection<CategoryGroup>();
        try
        {
            _developerGroup = _categoryGroups.First(cg => cg.Type == CategoryGroupType.Developer);
        }
        catch
        {
            _developerGroup = new CategoryGroup("CategoryService_Developer".GetLocalized(), CategoryGroupType.Developer);
            _categoryGroups.Add(_developerGroup);
        }

        try
        {
            _statusGroup = _categoryGroups.First(cg => cg.Type == CategoryGroupType.Status);
        }
        catch
        {
            _statusGroup = new CategoryGroup("CategoryService_Status".GetLocalized(), CategoryGroupType.Status);
            _categoryGroups.Add(_statusGroup);
        }
        
        // 将分类里的Galgame从string还原
        await Task.Run(() =>
        {
            foreach (CategoryGroup group in _categoryGroups)
            {
                foreach (Category c in group.Categories.OfType<Category>())
                    c.Galgames.ForEach(str =>
                    {
                        if (_galgameService.GetGalgameFromPath(str) is { } tmp)
                            SetBelong(tmp, c);
                    });
            }
        });
        
        _isInit = true;
    }

    public async Task<ObservableCollection<CategoryGroup>> GetCategoryGroupsAsync()
    {
        if (_isInit == false)
            await Init();
        return _categoryGroups;
    }

    private async void UpdateCategory(Galgame galgame)
    {
        if (_isInit == false) await Init();
        // 更新开发商分类组
        if (await _localSettings.ReadSettingAsync<bool>(KeyValues.AutoCategory) 
            && galgame.Developer.Value != Galgame.DefaultString && galgame.Developer.Value != string.Empty)
        {
            Category developer;
            try
            {
                developer = _developerGroup!.Categories.First(c =>
                    c.Name.Equals(galgame.Developer, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                developer = new Category(galgame.Developer.Value!);
                _developerGroup!.Categories.Add(developer);
            }
            SetBelong(galgame, developer);
        }
    }

    private async Task SaveAsync()
    {
        if (_isInit == false) return;
        foreach (CategoryGroup categoryGroup in _categoryGroups)
            categoryGroup.Categories.ForEach(c => c.UpdateSerializeList());
        await _localSettings.SaveSettingAsync(KeyValues.CategoryGroups, _categoryGroups, true);
    }
}