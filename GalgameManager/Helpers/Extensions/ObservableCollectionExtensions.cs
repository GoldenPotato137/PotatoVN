using System.Collections.ObjectModel;

namespace GalgameManager.Helpers;

public static class ObservableCollectionExtensions
{
    /// <summary>
    /// 将collection与other同步
    /// </summary>
    /// <param name="collection">待同步的ObservableCollection</param>
    /// <param name="other">要匹配的列表</param>
    /// <typeparam name="T"></typeparam>
    public static void SyncCollection<T>(this ObservableCollection<T> collection, IList<T> other)
    {
        // var delta = other.Count - collection.Count;
        // for (var i = 0; i < delta; i++)
        //     collection.Add(other[0]); //内容不总要，只是要填充到对应的总数
        // for (var i = delta; i < 0; i++)
        //     collection.RemoveAt(collection.Count - 1);
        //
        // for (var i = 0; i < other.Count; i++) 
        //     collection[i] = other[i];

        HashSet<T> toRemove = new(collection.Where(obj => !other.Contains(obj)));
        HashSet<T> toAdd = new(other.Where(obj => !collection.Contains(obj)));
        foreach (T obj in toRemove)
            collection.Remove(obj);
        foreach(T obj in toAdd)
            collection.Add(obj);
    }
}