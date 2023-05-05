using System.Text.Json.Serialization;

namespace GalgameManager.Models;

public class Faq
{
    [JsonInclude] public string Title;
    [JsonInclude] public string Content;

    public Faq(string title, string content)
    {
        Title = title;
        Content = content;
    }
}