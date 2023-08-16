namespace GalgameManager.Models;

public class Faq
{
    public string Title {get;set;}
    public string Content {get;set;}

    public Faq(string title, string content)
    {
        Title = title;
        Content = content;
    }
}