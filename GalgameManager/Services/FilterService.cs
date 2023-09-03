using GalgameManager.Contracts;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Filters;

namespace GalgameManager.Services;

public class FilterService : IFilterService
{
    public readonly List<IFilter> Filters;
    public event VoidDelegate? OnFilterChanged;
    private readonly ILocalSettingsService _localSettingsService;

    public FilterService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        _localSettingsService.OnSettingChanged += async (key, _) => await OnSettingChangedAsync(key);
        Filters = new List<IFilter>();
    }

    public async Task InitAsync()
    {
        await SetFiltersAsync();
    }
    
    public bool ApplyFilters(Galgame galgame)
    {
        return Filters.All(filter => filter.Apply(galgame));
    }

    public void AddFilter(IFilter filter)
    {
        if (Filters.Contains(filter)) return;
        Filters.Add(filter);
        OnFilterChanged?.Invoke();
    }

    public void RemoveFilter(IFilter filter)
    {
        if (Filters.Contains(filter) == false) return;
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
        Filters.Add((Activator.CreateInstance(type) as IFilter)!);
        OnFilterChanged?.Invoke();
    }

    private void RemoveFilter(Type type)
    {
        if (Filters.Any(filter => filter.GetType() == type) == false) return;
        Filters.RemoveAll(filter => filter.GetType() == type);
        OnFilterChanged?.Invoke();
    }
}