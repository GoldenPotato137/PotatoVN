namespace GalgameManager.Server.Models;

public class PagedResult<T>
{
    public int Cnt { get; set; }
    public int PageCnt { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public List<T> Items { get; set; }

    public PagedResult(List<T> items, int pageIndex, int pageSize, int count)
    {
        Items = items.Count <= pageSize ? items : items.Skip(pageIndex * pageSize).Take(pageSize).ToList();
        Cnt = count;
        PageCnt = (int)Math.Ceiling(count / (double)pageSize);
        PageIndex = pageIndex;
        PageSize = pageSize;
    }

    public PagedResult(IEnumerable<T> items, int pageIndex, int pageSize, int count) 
        : this(items.ToList(), pageIndex, pageSize, count)
    {
    }
}