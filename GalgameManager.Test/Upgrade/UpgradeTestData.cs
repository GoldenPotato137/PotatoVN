using Newtonsoft.Json.Linq;

namespace GalgameManager.Test.Upgrade;

internal sealed record ExpectedCategory(Guid? Id, string Name, int GameCount);

internal sealed record ExpectedCategoryGroup(
    Guid? Id,
    string DisplayName,
    int Type,
    IReadOnlyList<ExpectedCategory> Categories);

internal static class UpgradeTestData
{
    private static string? _workspaceRoot;

    public static string WorkspaceRoot => _workspaceRoot ??= FindWorkspaceRoot();

    public static string GetFixtureDir(string version)
        => Path.Combine(WorkspaceRoot, "GalgameManager.Test", "TestData", version);

    public static void CopyJsonOnly(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*.json", SearchOption.TopDirectoryOnly))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
    }

    public static IReadOnlyList<string> ReadExpectedGalgameNames(string fixtureDir)
    {
        var dataPath = Path.Combine(fixtureDir, "data.galgames.json");
        JToken galgames = File.Exists(dataPath)
            ? JArray.Parse(File.ReadAllText(dataPath))
            : ReadLocalSettingsProperty(fixtureDir, "galgames") ?? new JArray();

        return ReadObjects(galgames)
            .Select(ReadName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();
    }

    public static IReadOnlyList<ExpectedCategoryGroup> ReadExpectedCategoryGroups(string fixtureDir)
    {
        var dataPath = Path.Combine(fixtureDir, "data.categoryGroups.json");
        JToken categoryGroups = File.Exists(dataPath)
            ? JArray.Parse(File.ReadAllText(dataPath))
            : ReadLocalSettingsProperty(fixtureDir, "categoryGroups") ?? new JArray();

        return ReadObjects(categoryGroups)
            .Select(group =>
            {
                var type = ReadCategoryGroupType(group);
                var categories = ReadObjects(GetPropertyValue(group, "Categories") ?? new JArray())
                    .Select(category => new ExpectedCategory(
                        ReadGuid(category),
                        ReadName(category) ?? string.Empty,
                        ReadCategoryGameCount(category)))
                    .Where(category => !string.IsNullOrWhiteSpace(category.Name))
                    .ToList();
                return new ExpectedCategoryGroup(
                    ReadGuid(group),
                    ReadCategoryGroupDisplayName(group) ?? string.Empty,
                    type,
                    categories);
            })
            .Where(group => !string.IsNullOrWhiteSpace(group.DisplayName))
            .ToList();
    }

    public static IReadOnlyList<string> ReadExpectedGalgameSourceNames(string fixtureDir)
    {
        var dataPath = Path.Combine(fixtureDir, "data.galgameSources.json");
        if (File.Exists(dataPath))
        {
            return ReadObjects(JArray.Parse(File.ReadAllText(dataPath)))
                .Select(source => ReadFolderName(source) ?? ReadName(source))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList();
        }

        JToken folders = ReadLocalSettingsProperty(fixtureDir, "galgameFolders") ?? new JArray();
        return ReadObjects(folders)
            .Select(folder => ReadName(folder) ?? ReadFolderName(folder))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();
    }

    private static IEnumerable<JObject> ReadObjects(JToken token)
    {
        if (token is JObject single) return [single];
        return token is JArray array ? array.OfType<JObject>() : [];
    }

    private static string? ReadName(JObject item)
    {
        JToken? name = GetPropertyValue(item, "Name");
        return name switch
        {
            JValue { Type: JTokenType.String } value => value.Value<string>(),
            JObject obj => GetPropertyValue(obj, "Value")?.Value<string>(),
            _ => null,
        };
    }

    private static string? ReadCategoryGroupDisplayName(JObject group)
    {
        return ReadName(group);
    }

    private static int ReadCategoryGroupType(JObject group)
    {
        JToken? type = GetPropertyValue(group, "Type");
        if (type?.Type == JTokenType.Integer) return type.Value<int>();
        return type?.Value<string>() switch
        {
            "Developer" => 0,
            "Status" => 1,
            "Custom" => 2,
            "Engine" => 3,
            _ => -1,
        };
    }

    private static int ReadCategoryGameCount(JObject category)
    {
        JToken? games = GetPropertyValue(category, "GalgamesX") ?? GetPropertyValue(category, "Galgames");
        return games is JArray array ? array.Count : 0;
    }

    private static Guid? ReadGuid(JObject item)
    {
        var value = GetPropertyValue(item, "Id")?.Value<string>();
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static string? ReadFolderName(JObject folder)
    {
        var path = GetPropertyValue(folder, "Path")?.Value<string>();
        return string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static JToken? ReadLocalSettingsProperty(string fixtureDir, string propertyName)
    {
        var path = Path.Combine(fixtureDir, "LocalSettings.json");
        Assert.That(File.Exists(path), Is.True, $"未找到 LocalSettings.json: {path}");
        return GetPropertyValue(JObject.Parse(File.ReadAllText(path)), propertyName);
    }

    private static JToken? GetPropertyValue(JObject obj, string propertyName)
    {
        return obj.Properties()
            .FirstOrDefault(property => property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string FindWorkspaceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "GalgameManager.sln");
            if (File.Exists(candidate)) return dir.FullName;
        }

        Assert.Fail($"未找到工作区根目录（GalgameManager.sln）。当前测试目录: {AppContext.BaseDirectory}");
        return string.Empty;
    }
}
