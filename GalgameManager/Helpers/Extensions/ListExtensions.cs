namespace GalgameManager.Helpers;

public static class ListExtensions
{
    public static int RemoveNull<T>(this List<T> list)
    {
        return list.RemoveAll(item => item is null);
    }
}