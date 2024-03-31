using GalgameManager.Models;
using GalgameManager.Models.Sources;
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
        JProperty? type = jObject.Property(nameof(GalgameSourceBase.SourceType));
        if (type != null && type.Count > 0)
        {
            var typeValue = type.Value.ToString();
            GalgameSourceType menuButtonType = (GalgameSourceType)Enum.Parse(typeof(GalgameSourceType), typeValue);
            switch (menuButtonType)
            {
                case GalgameSourceType.UnKnown:
                    throw new NotSupportedException();
                case GalgameSourceType.LocalFolder:
                    target = new GalgameFolderSource();
                    break;
                case GalgameSourceType.LocalZip:
                    target = new GalgameZipSource();
                    break;
                case GalgameSourceType.Virtual:
                    throw new NotSupportedException();
                default:
                    throw new NotSupportedException();
            }
        }
        serializer.Populate(jObject.CreateReader(), target);
        return target;
    }
}