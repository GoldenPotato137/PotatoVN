using System.Collections.ObjectModel;
using GalgameManager.Contracts;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;

namespace GalgameManager.Services;

public class FilterService : IFilterService
{
    public readonly ObservableCollection<IFilter> Filters;
    public event VoidDelegate? OnFilterChanged;

    public FilterService(ILocalSettingsService localSettingsService)
    {
        Filters = localSettingsService.ReadSettingAsync<ObservableCollection<IFilter>>(KeyValues.Filters, true).Result
                  ?? new ObservableCollection<IFilter>();
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
}