using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GalgameManager.Helpers.API;

public class VndbQuery
{
    [JsonConverter(typeof(VndbFilters.VndbFiltersJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("filters")]
    public VndbFilters? Filters { get; set; }

    [JsonInclude]
    [JsonPropertyName("fields")]
    public string Fields = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("sort")]
    public string? Sort = null;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("reverse")] public bool? Reverse = null;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("results")] public int? Results = null;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("page")] public int? Page = null;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("user")] public object? User = null;
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("count")] public bool? Count = null;
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("compact_filters")] public bool? CompactFilters = null;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("normalized_filters")]
    public bool? NormalizedFilters = null;
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
