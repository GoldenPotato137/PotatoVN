using System.Threading.Tasks;
using GalgameManager.Models.LLM;

namespace GalgameManager.WinApp.Base.Contracts;

public interface ILlMService
{
    /// <summary>
    /// Ask a question to the AI.
    /// </summary>
    /// <param name="prompt">The user prompt.</param>
    /// <returns>The AI response message.</returns>
    Task<ChatMessage> ChatAsync(string prompt);
}
