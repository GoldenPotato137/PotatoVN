using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Filters;

namespace GalgameManager.Services;

public class FilterService : IFilterService
{
    public ObservableCollection<FilterBase> Filters;
    public event Action? OnFilterChanged;
    private readonly ILocalSettingsService _localSettingsService;

    public FilterService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        _localSettingsService.OnSettingChanged += async (key, _) => await OnSettingChangedAsync(key);
        Filters = new ObservableCollection<FilterBase>();
    }

    public async Task InitAsync()
    {
        await SetFiltersAsync();
    }

    public ObservableCollection<FilterBase> GetFilters() => Filters;

    public bool ApplyFilters(Galgame galgame)
    {
        return Filters.All(filter => filter.Apply(galgame));
    }

    public void AddFilter(FilterBase filter)
    {
        if (Filters.Contains(filter)) return;
        Filters.Add(filter);
        if (filter is VirtualGameFilter)
            _localSettingsService.SaveSettingAsync(KeyValues.DisplayVirtualGame, false);
        OnFilterChanged?.Invoke();
    }

    public void RemoveFilter(FilterBase filter)
    {
        if (Filters.Contains(filter) == false) return;
        Filters.Remove(filter);
        if (filter is VirtualGameFilter)
            _localSettingsService.SaveSettingAsync(KeyValues.DisplayVirtualGame, true);
        OnFilterChanged?.Invoke();
    }

    public void ClearFilters()
    {
        List<FilterBase> toRemove = Filters.Where(filter => filter is not VirtualGameFilter).ToList();
        foreach (FilterBase filter in toRemove)
            Filters.Remove(filter);
        OnFilterChanged?.Invoke();
    }

    private async Task OnSettingChangedAsync(string key)
    {
        if (key == KeyValues.DisplayVirtualGame)
            await SetFiltersAsync();
    }
    
    private async Task SetFiltersAsync()
    {
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayVirtualGame))
            RemoveFilter(typeof(VirtualGameFilter));
        else
            AddFilter(typeof(VirtualGameFilter));
    }

    private void AddFilter(Type type)
    {
        if (Filters.Any(filter => filter.GetType() == type)) return;
        Filters.Add((Activator.CreateInstance(type) as FilterBase)!);
        OnFilterChanged?.Invoke();
    }

    private void RemoveFilter(Type type)
    {
        if (Filters.Any(filter => filter.GetType() == type) == false) return;
        Filters.Remove(Filters.First(filter => filter.GetType() == type));
        OnFilterChanged?.Invoke();
    }

    public async Task<List<FilterBase>> SearchFilters(string str)
    {
        List<FilterBase> result = new();
        if (str.Contains('/'))
            str = str[..(str.LastIndexOf('/') - 1)];
        await Task.Run((async Task() =>
        {
            List<Galgame> games = (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!.Galgames;
            IEnumerable<CategoryGroup> categoryGroups = await App.GetService<ICategoryService>().GetCategoryGroupsAsync();
            //Category
            HashSet<string> addedCategories = new();
            result.AddRange(from categoryGroup in categoryGroups
                from category in categoryGroup.Categories
                where category.Name.ContainX(str)
                where addedCategories.Add(category.Name)
                select new CategoryFilter(category));
            result.RemoveAll(filter => Filters.Any(f => f is CategoryFilter && f.Name == filter.Name));
            //Tags
            HashSet<string> addedTags = new();
            result.AddRange(from game in games
                from tag in game.Tags.Value ?? new ObservableCollection<string>()
                where tag.ContainX(str)
                where addedTags.Add(tag)
                select new TagFilter(tag));
            result.RemoveAll(filter => Filters.Any(f => f is TagFilter && f.Name == filter.Name));
            //本地游戏
            if(Filters.Any(f => f is VirtualGameFilter) == false)
                result.Add(new VirtualGameFilter());
        })!);
        return result;
    }
}