using System.Drawing.Text;
using Windows.Foundation;

namespace GalgameManager.Helpers;

public static class Utils
{
    public static string GetFirstValueByNameOrEmpty(this WwwFormUrlDecoder decoder, string name)
    {
        try
        {
            return decoder.GetFirstValueByName(name);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 检查字体是否安装
    /// </summary>
    /// <param name="fontName">字体名</param>
    public static bool IsFontInstalled(string fontName)
    {
        InstalledFontCollection fontsCollection = new();
        return fontsCollection.Families.Any(font => font.Name.Equals(fontName, StringComparison.InvariantCultureIgnoreCase));
    }
}