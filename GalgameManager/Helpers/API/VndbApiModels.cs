using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace GalgameManager.Helpers.API;

public class VndbQuery
{
    [JsonConverter(typeof(VndbFilters.VndbFiltersJsonConverter))]
    public VndbFilters? Filters { get; set; }
    public string? Fields { get; set; }
    public string? Sort { get; set; }
    public bool? Reverse { get; set; }
    public int? Results { get; set; }
    public int? Page { get; set; }
    public object? User { get; set; }
    public bool? Count { get; set; }
    public bool? CompactFilters { get; set; }
    public bool? NormalizedFilters { get; set; }
}

public class VndbResponse
{
    public JsonArray? Results { get; set; }
    public bool? More { get; set; }
    public int? Count { get; set; }
    public string? CompactFilters { get; set; }
    public VndbFilters? NormalizedFilters { get; set; }
}

public class VndbProducer
{
    public string Id { get; set; }
    public string? Name { get; set; }
    public string? Original { get; set; }
    public IList<string>? Aliases { get; set; }
    public string? Lang { get; set; }
    public string? Type { get; set; }
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
