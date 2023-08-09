using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GalgameManager.Helpers.API;

public class VndbQuery
{
    [JsonConverter(typeof(VndbFilters.VndbFiltersJsonConverter))]
    [JsonPropertyName("filters")]
    public VndbFilters? Filters { get; set; }
    [JsonPropertyName("fields")]
    public string? Fields { get; set; }
    [JsonPropertyName("sort")]
    public string? Sort { get; set; }
    [JsonPropertyName("reverse")] public bool? Reverse { get; set; }
    [JsonPropertyName("results")] public int? Results { get; set; }
    [JsonPropertyName("page")] public int? Page { get; set; }
    [JsonPropertyName("user")] public object? User { get; set; }
    [JsonPropertyName("count")] public bool? Count { get; set; }
    [JsonPropertyName("compact_filters")] public bool? CompactFilters { get; set; }
    [JsonPropertyName("normalized_filters")]public bool? NormalizedFilters { get; set; }
}

public class VndbResponse
{
    [JsonPropertyName("results")]
    public JsonArray? Results { get; set; }
    [JsonPropertyName("more")]
    public bool? More { get; set; }
    [JsonPropertyName("count")]
    public int? Count { get; set; }
    [JsonPropertyName("compact_filters")]
    public string? CompactFilters { get; set; }
    [JsonPropertyName("normalized_filters")]
    public VndbFilters? NormalizedFilters { get; set; }
}

public class VndbProducer
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("original")]
    public string? Original { get; set; }
    [JsonPropertyName("aliases")]
    public IList<string>? Aliases { get; set; }
    [JsonPropertyName("lang")]
    public string? Lang { get; set; }
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class SnakeNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        var snake_name = "";
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && name[i] >= 'A' && name[i] <= 'Z')
            {
                snake_name += "_" + name[i].ToString().ToLower();
            }else
            {
                snake_name += name[i].ToString().ToLower();
            }
        }

        return snake_name;
    }
}

public class VndbFilters
{
    private JsonArray? _jsonArray;

    public VndbFilters(JsonArray jsonArray)
    {
        _jsonArray = jsonArray;
    }

    public VndbFilters()
    {
        _jsonArray = null;
    }

    public static VndbFilters And(params VndbFilters[] vndbFiltersArray)
    {
        JsonArray jsonArray = new("and");
        foreach (VndbFilters vndbFilters in vndbFiltersArray)
        {
            jsonArray.Add(vndbFilters._jsonArray);
        }

        return new VndbFilters(jsonArray);
    }

    public static VndbFilters Or(params VndbFilters[] vndbFiltersArray)
    {
        JsonArray jsonArray = new("or");
        foreach (VndbFilters vndbFilters in vndbFiltersArray)
        {
            jsonArray.Add(vndbFilters._jsonArray);
        }

        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters Equal(string name, string? value)
    {
        JsonArray jsonArray = new(name, "=", value);
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters Greater(string name, string? value)
    {
        JsonArray jsonArray = new(name, ">", value);
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters GreaterEqual(string name, string? value)
    {
        JsonArray jsonArray = new(name, ">=", value);
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters Less(string name, string? value)
    {
        JsonArray jsonArray = new(name, "<", value);
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters LessEqual(string name, string? value)
    {
        JsonArray jsonArray = new(name, "<=", value);
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters NotEqual(string name, string? value)
    {
        JsonArray jsonArray = new(name, "!=", value);
        return new VndbFilters(jsonArray);
    }
    
    public class VndbFiltersJsonConverter : JsonConverter<VndbFilters>
    {
        public override VndbFilters Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return new VndbFilters(JsonNode.Parse(ref reader)!.AsArray());
        }

        public override void Write(
            Utf8JsonWriter writer,
            VndbFilters vndbFiltersValue,
            JsonSerializerOptions options)
        {
            if (vndbFiltersValue._jsonArray is not null)
            {
                vndbFiltersValue._jsonArray.WriteTo(writer);
                return;
            }
            writer.WriteNullValue();
        }
    }
}
