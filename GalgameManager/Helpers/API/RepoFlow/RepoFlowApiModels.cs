using Newtonsoft.Json;

namespace GalgameManager.Helpers.API.RepoFlow;

public class Repository
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("packageType")] public string PackageType { get; set; } = string.Empty;
    [JsonProperty("repositoryType")] public string RepositoryType { get; set; } = string.Empty;
    [JsonProperty("status")] public string Status { get; set; } = string.Empty;
}

public class PackageMeta
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("type")] public string Type { get; set; } = string.Empty;
    [JsonProperty("versions")] public List<PackageVersion> Versions { get; set; } = [];
    
    public Version LatestVersion => Versions
        .Select(v => v.Version)
        .OrderByDescending(v => v)
        .FirstOrDefault() ?? new Version();
}

public class PackageVersion
{
    [JsonProperty("versionId")] public string VersionId { get; set; } = string.Empty;
    [JsonProperty("version")] public Version Version { get; set; } = new();
}

public class PackageDetailVersion : PackageVersion
{
    [JsonProperty("createdAt")] public DateTime CreatedAt { get; set; }
    [JsonProperty("downloads")] public long Downloads { get; set; }
}