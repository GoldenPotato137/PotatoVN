using System.Collections.ObjectModel;
using System.Collections.Specialized;
using GalgameManager.Helpers;

namespace GalgameManager.Models;

public delegate bool SearchPredicate<in T>(T obj, string searchString);
public class DisplayCollection<T> : ObservableCollection<T>, INotifyCollectionChanged where T : IComparable<T>
{
    private readonly ObservableCollection<T> _baseCollection;
    public Predicate<T>? Filter { get; set; }

    public SearchPredicate<T>? ApplySearchKey { get; set; }

    private string? _searchKey;
    
    private bool CheckDisplay(T item)
    {
        if (_searchKey.IsNullOrEmpty())
            return Filter == null || Filter(item);
        return ApplySearchKey == null || ApplySearchKey(item, _searchKey ?? "");
    }
    
    public DisplayCollection(ObservableCollection<T> baseCollection)
    {
        _baseCollection = baseCollection;
        foreach (T item in _baseCollection)
        {
            TryAddToDisplay(item);
        }
        _baseCollection.CollectionChanged += BaseCollectionOnCollectionChanged;
    }

    public void Refresh()
    {
        Clear();
        foreach (T item in _baseCollection)
        {
            TryAddToDisplay(item);
        }
    }
    
    public DisplayCollection()
    {
        _baseCollection = new ObservableCollection<T>();
        _baseCollection.CollectionChanged += BaseCollectionOnCollectionChanged;
    }

    private void BaseCollectionOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems == null) break;
                foreach (var newItem in e.NewItems)
                {
                    if (newItem is T item)
                    {
                        TryAddToDisplay(item);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems == null) break;
                foreach (var oldItem in e.OldItems)
                {
                    if (oldItem is T item)
                    {
                        TryRemoveFromDisplay(item);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Reset:
            case NotifyCollectionChangedAction.Replace:
                if (e.NewItems != null){
                    foreach (var newItem in e.NewItems)
                    {
                        if (newItem is T item)
                        {
                            TryAddToDisplay(item);
                        }
                    }
                }

                if (e.OldItems != null)
                {
                    foreach (var oldItem in e.OldItems)
                    {
                        if (oldItem is T item)
                        {
                            TryRemoveFromDisplay(item);
                        }
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void ApplyFilter()
    {
        Clear();
        foreach(T tmp in _baseCollection)
            TryAddToDisplay(tmp);
    }

    public void ApplySearch(string searchKey)
    {
        _searchKey = searchKey;
        foreach (T tmp in _baseCollection)
        {
            if(!CheckDisplay(tmp) && _searchKey!=string.Empty)
                TryRemoveFromDisplay(tmp);
            else
                TryAddToDisplay(tmp);
        }
    }

    private void TryAddToDisplay(T item)
    {
        if (Contains(item)) return;
        if (!CheckDisplay(item)) return;
        for(var i = 0;i < Count;i++) //这里可以用二分查找优化, 暂时不做
            if (item.CompareTo(this[i]) >= 0)
            {
                Insert(i, item);
                return;
            }
        Add(item);
    }

    private void TryRemoveFromDisplay(T item)
    {
        if (Contains(item)==false) return;
        Remove(item);
    }

}