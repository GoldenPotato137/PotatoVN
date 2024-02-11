namespace GalgameManager.Helpers;

public static class EnumExtension
{
    public static string GetLocalized(this Enum e)
    {
        return $"{e.GetType().Name}_{e.ToString()}".GetLocalized();
    }
}