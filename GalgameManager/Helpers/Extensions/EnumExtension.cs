using GalgameManager.Contracts.Services;

namespace GalgameManager.Helpers;

public static class EnumExtension
{
    private static readonly Dictionary<Type, Dictionary<int, string>> AddedEnum = [];
    
    public static string GetLocalized(this Enum e)
    {
        try
        {
            if (AddedEnum.TryGetValue(e.GetType(), out Dictionary<int, string>? map) 
                && map.TryGetValue(Convert.ToInt32(e), out var localized))
                return localized;
        
            return $"{e.GetType().Name}_{e.ToString()}".GetLocalized();
        }
        catch (Exception exception)
        {
            App.GetService<IInfoService>().DeveloperEvent(e: exception);
        }
        return "Unknown Enum";
    }

    public static void Register(Type enumType, int value, string localized)
    {
        if (!enumType.IsEnum) throw new ArgumentException("Type must be an enum");
        if (!AddedEnum.TryGetValue(enumType, out Dictionary<int, string>? map))
        {
            map = [];
            AddedEnum[enumType] = map;
        }
        map[value] = localized;
    }
}