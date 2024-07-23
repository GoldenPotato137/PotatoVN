using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Helpers.API;

public class VndbQuery
{
    [JsonConverter(typeof(VndbFilters.VndbFiltersConverter))]
    public VndbFilters? Filters { get; set; }
    public string? Fields { get; set; }
    public string? Sort { get; set; }
    public bool? Reverse { get; set; }
    public int? Results { get; set; }
    public int? Page { get; set; }
    public string? User { get; set; }
    public bool? Count { get; set; }
    public bool? CompactFilters { get; set; }
    public bool? NormalizedFilters { get; set; }
}

public class VndbResponse<T>
{
    public List<T>? Results { get; set; }
    public bool? More { get; set; }
    public int? Count { get; set; }
    public string? CompactFilters { get; set; }
    public VndbFilters? NormalizedFilters { get; set; }
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
public class VndbVn
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    // Alternative title
    public string? Alttitle { get; set; }
    public List<VndbTitle>? Titles { get; set; }
    public List<string>? Aliases { get; set; }
    // language the VN has originally been written in. 
    public string? Olang { get; set; }
    public DevStatusEnum? Devstatus { get; set; }
    public string? Released { get; set; }
    public List<string>? Languages { get; set; }
    public List<string>? Platforms { get; set; }
    public VndbImage? Image { get; set; }
    public VnLenth? Lenth { get; set; }
    public int? LengthMinutes { get; set; }
    public int? LengthVotes { get; set; }
    public string? Description { get; set; }
    public float? Rating { get; set; }
    public int? Votecount { get; set; }
    public List<VnTag>? Tags { get; set; }
    public List<VndbProducer>? Developers { get; set; }
    
    // Only with character
    
    public VndbRole? Role { get; set; }
    
    public int? Spoiler { get; set; }
    
    public enum VnLenth
    {
        VeryShort = 1,
        Short = 2,
        Medium = 3,
        Long = 4,
        VeryLong = 5
    }
    
    public enum DevStatusEnum
    {
        Finished=0,
        InDevelopment=1,
        Cancelled=2
    }
    
    public enum VndbRole
    {
        Main,
        Primary,
        Side,
        Appears
    }
}

public class VndbProducer
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Original { get; set; }
    public List<string>? Aliases { get; set; }
    public string? Lang { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
}

public class VndbImage
{
    public string? Id { get; set; }
    public string? Url { get; set; }
    public int[]? Dims { get; set; }
    public float? Sexual { get; set; }
    public float? Violence { get; set; }
}

public class VndbScreenshot : VndbImage
{
    public string? Thumbnail { get; set; }
    public int[]? ThumbnailDims { get; set; }
    // todo: Add screenshots.release.*
}

public class VndbTag
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public List<string>? Aliases { get; set; }
    public string? Description { get; set; }
    // "cont" for content, "ero" for sexual content and "tech" for technical tags. 
    public string? Category { get; set; }
    public bool? Searchable { get; set; }
    public bool? Applicable { get; set; }
    public int? VnCount { get; set; }
}

public class VnTag : VndbTag
{
    public float? Rating { get; set; }
    public int? Spoiler { get; set; }
    public bool? Lie { get; set; }
}



public class VndbTitle
{
    public string? Lang { get; set; }
    public string? Title { get; set; }
    public string? Latin { get; set; }
    public bool? Official { get; set; }
    public bool? Main { get; set; }
}

public class VndbCharacter
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Original { get; set; }
    public List<string>? Aliases { get; set; }
    public string? Description { get; set; }
    public VndbImage? Image { get; set; }
    public string? BloodType { get; set; }
    public int? Height { get; set; } 
    public int? Weight { get; set; }
    public int? Bust { get; set; }
    public int? Waist { get; set; }
    public int? Hips { get; set; }
    public string? Cup { get; set; }
    public int? Age { get; set; }
    public int[]? Birthday { get; set; }
    public string[]? Sex { get; set; }
    public List<VndbVn>? Vns { get; set; }
}
public class VndbFilters
{
    private readonly JArray? _jsonArray;

    private VndbFilters(JArray jsonArray)
    {
        _jsonArray = jsonArray;
    }

    public VndbFilters()
    {
        _jsonArray = null;
    }

    public static VndbFilters And(params VndbFilters[] vndbFiltersArray)
    {
        JArray jsonArray = new("and");
        foreach (VndbFilters vndbFilters in vndbFiltersArray)
        {
            if (vndbFilters._jsonArray != null) jsonArray.Add(vndbFilters._jsonArray);
        }

        return new VndbFilters(jsonArray);
    }

    public static VndbFilters Or(params VndbFilters[] vndbFiltersArray)
    {
        JArray jsonArray = new("or");
        foreach (VndbFilters vndbFilters in vndbFiltersArray)
        {
            if (vndbFilters._jsonArray != null) jsonArray.Add(vndbFilters._jsonArray);
        }

        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters Equal(string name, string value)
    {
        JArray jsonArray = new(name, "=", value);
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters Equal(string name, VndbFilters value)
    {
        JArray jsonArray = new(name, "=", value._jsonArray ?? new JArray());
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters Greater(string name, string value)
    {
        JArray jsonArray = new(name, ">", value);
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters GreaterEqual(string name, string value)
    {
        JArray jsonArray = new(name, ">=", value);
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters Less(string name, string value)
    {
        JArray jsonArray = new(name, "<", value);
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters LessEqual(string name, string value)
    {
        JArray jsonArray = new(name, "<=", value);
        return new VndbFilters(jsonArray);
    }
    
    public static VndbFilters NotEqual(string name, string value)
    {
        JArray jsonArray = new(name, "!=", value);
        return new VndbFilters(jsonArray);
    }
    
    public class VndbFiltersConverter : JsonConverter<VndbFilters>
    {
        public override void WriteJson(JsonWriter writer, VndbFilters? value, JsonSerializer serializer)
        {
            value?._jsonArray?.WriteTo(writer);
        }

        public override VndbFilters? ReadJson(JsonReader reader, Type objectType, VndbFilters? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            try
            {
                return new VndbFilters(JArray.Load(reader));
            }
            catch
            {
                return null;
            }
        
        }
    }
}

public class UserLabelsResponse
{
    public required List<UserLabel> Labels { get; set; }
}

public class UserLabel
{
    public int Id { get; set; }
    public string? Label { get; set; }
    public bool Private { get; set; }
}

public class AuthInfoResponse
{
    public required string Id { get; set; }
    public required string Username { get; set; }
    
    public required List<VndbApiPermission> Permissions { get; set; }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum VndbApiPermission
{
    [EnumMember(Value = "listread")]
    ListRead, 
    [EnumMember(Value = "listwrite")]
    ListWrite
}

public class PatchUserListRequest
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? Vote { get; set; } = null;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Notes { get; set; } = null;
    /// <summary>
    /// Started Date
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Started  { get; set; } = null;
    /// <summary>
    /// Finished Date
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Finished  { get; set; } = null;
    /// <summary>
    /// 覆盖所有Labels
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? Labels  { get; set; } = null;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? LabelsSet  { get; set; } = null;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<int>? LabelsUnset  { get; set; } = null;
}

public class VndbUserListItem
{
    public string? Id { get; set; }
    public int? Added { get; set; }
    public int? Voted { get; set; }
    public int? Lastmod { get; set; }
    public int? Vote { get; set; }
    public string? Started { get; set; }
    public string? Finished { get; set; }
    public string? Notes { get; set; }
    public List<UserLabel>? Labels { get; set; }
    public VndbVn? Vn { get; set; }
    //TODO: Add releases
}



