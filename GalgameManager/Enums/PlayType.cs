using GalgameManager.Helpers;

namespace GalgameManager.Enums;

public enum PlayType
{
    None,
    Playing,
    Played,
    Shelved,
    Abandoned,
}

public static class PlayTypeHelper
{
    public static string GetLocalized(this PlayType playType)
    {
        return ("PlayType_" + playType).GetLocalized();
    }
}