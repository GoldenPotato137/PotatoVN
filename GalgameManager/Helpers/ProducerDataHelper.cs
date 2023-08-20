using System.Reflection;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Helpers;

public static class ProducerDataHelper
{
    private const string ProducerFile = @"Assets\Data\producers.json";
    private static bool _isInit = false;
    private static List<Producer> _producers = new ();
    private static void Init()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        var file = Path.Combine(Path.GetDirectoryName(assembly.Location)!, ProducerFile);
        if (!File.Exists(file)) return;

        JToken json = JToken.Parse(File.ReadAllTextAsync(file).Result);
        List<JToken>? producersJson = json.ToObject<List<JToken>>();
        producersJson!.ForEach(dev =>
        {
            if (!string.IsNullOrEmpty(dev["name"]!.ToString()))
            {
                _producers.Add(
                    new Producer(
                        dev["id"]!.ToString(), 
                        dev["name"]!.ToString(), 
                        dev["latin"]!.ToString(), 
                        dev["alias"]!.ToString().Split("\n").ToList()
                        )
                    );
            }
        });
        _isInit = true;
    }

    public static List<Producer> Producers
    {
        get
        {
            if (!_isInit) Init();
            return _producers;
        }
    }
}

public class Producer
{
    /// <summary>
    /// Id: string, start with p
    /// eg: p123
    /// </summary>
    public string Id;
    public string Name;
    public string Latin;
    public List<string> Alias;
    public List<string> Names;

    public Producer(string id, string name, string latin, List<string> alias)
    {
        Id = id;
        Name = name;
        Latin = latin;
        Alias = alias;
        Names = new List<string>();
        if (!string.IsNullOrEmpty(name)) Names.Add(name);
        if (!string.IsNullOrEmpty(latin)) Names.Add(latin);
        Names.AddRange(Alias);
    }

    public Producer(string name)
    {
        Id = string.Empty;
        Name = name;
        Latin = string.Empty;
        Alias = new List<string>();
        Names = new List<string> {name};
    }
}