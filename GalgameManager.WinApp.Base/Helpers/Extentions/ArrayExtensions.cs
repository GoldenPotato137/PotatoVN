namespace GalgameManager.Helpers;

public static class ArrayExtensions
{
    public static T[] ResizeArray<T>(this T[] original, int newSize)
    {
        T[] resizedArray = new T[newSize];
        original.CopyTo(resizedArray, 0);
        return resizedArray;
    }
}