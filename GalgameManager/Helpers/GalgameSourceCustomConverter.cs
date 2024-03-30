using GalgameManager.Contracts.Models;
using GalgameManager.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Helpers;

public class GalgameSourceCustomConverter:CustomCreationConverter<GalgameSourceBase>
{
    public override GalgameSourceBase Create(Type objectType)
    {
        return new GalgameSourceBase();
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        JObject jObject = JObject.Load(reader);
        GalgameSourceBase? target = new();
        JProperty? type = jObject.Property("GalgameSourceType");
        if (type != null && type.Count > 0)
        {
            var typeValue = type.Value.ToString();
            SourceType menuButtonType = (SourceType)Enum.Parse(typeof(SourceType), typeValue);
            switch (menuButtonType)
            {
                case SourceType.UnKnown:
                    throw new NotSupportedException();
                case SourceType.LocalFolder:
                    target = new GalgameFolderSource();
                    break;
                case SourceType.LocalZip:
                    target = new GalgameZipSource();
                    break;
                case SourceType.Virtual:
                    throw new NotSupportedException();
                default:
                    throw new NotSupportedException();
            }
        }
        serializer.Populate(jObject.CreateReader(), target);
        return target;
    }
}