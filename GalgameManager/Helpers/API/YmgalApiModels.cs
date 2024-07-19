namespace GalgameManager.Helpers.API;


public class OauthRequest
{
    public required string AccessToken { get; set; }
    public required string TokenType { get; set; }
    public required int ExpiresIn { get; set; }
    public required string Scope { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public int Code { get; set; }
    public required string Msg { get; set; }
    public T? Data { get; set; }
}

public class Archive
{
    public int PublishVersion { get; set; }
    public required string PublishTime { get; set; }
    public int Publisher { get; set; }
    public required string Name { get; set; }
    public string? ChineseName { get; set; }
    public required ExtensionName[] ExtensionName { get; set; }
    public required string Introduction { get; set; }
    public required string State { get; set; }
    public required int Weights { get; set; }
    public required string MainImg { get; set; }
    public required MoreEntry[] MoreEntry { get; set; }
}

public class Game : Archive
{
    public int Gid { get; set; }
    public int Id { get; set; }
    public int DeveloperId { get; set; }
    public bool HaveChinese { get; set; }
    public required string TypeDesc { get; set; }
    public required string ReleaseDate { get; set; }
    public bool Restricted { get; set; }
    public required string Country { get; set; }
    public required Website[] Website { get; set; }
    public required Characters[] Characters { get; set; }
    public required Releases[] Releases { get; set; }
    public required Staff[] Staff { get; set; }
    public required string Type { get; set; }
    public bool Freeze { get; set; }
}

public class ExtensionName
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Desc { get; set; }
}

public class MoreEntry
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}

public class Website
{
    public required string Title { get; set; }
    public required string Link { get; set; }
}

public class Characters
{
    public int Cid { get; set; }
    public int CvId { get; set; }
    public int CharacterPosition { get; set; }
}

public class Releases
{
    public int Id { get; set; }
    public required string ReleaseName { get; set; }
    public required string RelatedLink { get; set; }
    public required string Platform { get; set; }
    public required string ReleaseDate { get; set; }
    public required string ReleaseLanguage { get; set; }
    public required string RestrictionLevel { get; set; }
}

public class Staff
{
    public int Sid { get; set; }
    public int Pid { get; set; }
    public required string EmpName { get; set; }
    public required string EmpDesc { get; set; }
    public required string JobName { get; set; }
}


public class GameResponse
{
    public required Game Game { get; set; }
    public required Dictionary<string, CidMapping> CidMapping { get; set; }
    public required Dictionary<string, PidMapping> PidMapping { get; set; }
}

public class CidMapping 
{
    public int Cid { get; set; }
    public required string Name { get; set; }
    public required string MainImg { get; set; }
    public required string State { get; set; }
    public bool Freeze { get; set; }
}

public class PidMapping
{
    public int Pid { get; set; }
    public required string Name { get; set; }
    public required string MainImg { get; set; }
    public required string State { get; set; }
    public bool Freeze { get; set; }
}

public class OrganizationResponse
{
    public required Organization Org { get; set; }
}

public class Organization
{
    public int PublishVersion { get; set; }
    public required string PublishTime { get; set; }
    public int Publisher { get; set; }
    public required string Name { get; set; }
    public required string ChineseName { get; set; }
    public required ExtensionName[] ExtensionName { get; set; }
    public required string Introduction { get; set; }
    public required string State { get; set; }
    public int Weights { get; set; }
    public required string MainImg { get; set; }
    public required object[] MoreEntry { get; set; }
    public int OrgId { get; set; }
    public required string Country { get; set; }
    public required Website[] Website { get; set; }
    public required string Type { get; set; }
    public bool Freeze { get; set; }
}

public class Page<T>
{
    public required List<T> Result { get; set; }
    public int Total { get; set; }
    public bool HasNext { get; set; }
    public int PageNum { get; set; }
    public int PageSize { get; set; }
}


