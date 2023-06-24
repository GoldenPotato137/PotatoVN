using System.Collections.ObjectModel;
using GalgameManager.Contracts;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Services;

public class CategoryService : ICategoryService
{
    private ObservableCollection<CategoryGroup> _categoryGroups = new();
    private ObservableCollection<ICategory> _categories = new();
    private bool _isInit;
    private readonly ILocalSettingsService _localSettings;

    public CategoryService(ILocalSettingsService localSettings, IDataCollectionService<Galgame> galgameService)
    {
        _localSettings = localSettings;
        ((GalgameCollectionService)galgameService).PhrasedEvent2 += TryCategory;
    }

    private async Task Init()
    {
        if (_isInit) return;
        _categoryGroups = await _localSettings.ReadSettingAsync<ObservableCollection<CategoryGroup>>
            (KeyValues.CategoryGroups, true) ?? new ObservableCollection<CategoryGroup>();
        _categories = await _localSettings.ReadSettingAsync<ObservableCollection<ICategory>>
            (KeyValues.Categories, true) ?? new ObservableCollection<ICategory>();
        _isInit = true;
    }

    public async Task<ObservableCollection<CategoryGroup>> GetCategoryGroupsAsync()
    {
        if (_isInit == false)
            await Init();
        return _categoryGroups;
    }

    private async void TryCategory(Galgame galgame)
    {
        if (await _localSettings.ReadSettingAsync<bool>(KeyValues.AutoCategory) == false) return;
    }
}