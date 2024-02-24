using System.Text;
using Newtonsoft.Json;

namespace GalgameManager.Core.Helpers;

public static class ObjectExtension
{
    public static StringContent ToJsonContent(this object obj)
    {
        return new StringContent(obj.ToJson(), Encoding.UTF8, "application/json");
    }
    
    public static string ToJson(this object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }
}