using GalgameManager.Enums;

namespace GalgameManager.Models.LLM;

public class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Name { get; set; }

    public ChatMessage() { }

    public ChatMessage(ChatRole role, string content)
    {
        Role = role;
        Content = content;
    }
}
