using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;
using Newtonsoft.Json;

namespace GalgameManager.Helpers;

public class GalgameAndUidConverter : JsonConverter<Galgame>
{
    public override void WriteJson(JsonWriter writer, Galgame? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value?.Uid);
    }

    public override Galgame? ReadJson(JsonReader reader, Type objectType, Galgame? existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        GalgameUid? uid = serializer.Deserialize<GalgameUid>(reader);
        if (uid is null) return null;
        return (App.GetService<IGalgameCollectionService>() as GalgameCollectionService)!.GetGalgameFromUid(uid);
    }
}