using System.Threading.Tasks;
using GalgameManager.Models.LLM;

namespace GalgameManager.WinApp.Base.Contracts;

/// <summary>
/// LLM服务接口
/// </summary>
public interface ILlMService
{
    /// <summary>
    /// 与LLM进行对话
    /// </summary>
    /// <param name="prompt">用户提问</param>
    /// <param name="models">可选的模型名称数组，如果传入则批量调用匹配的模型，不传则使用默认模型</param>
    /// <returns>单个AI响应或批量响应</returns>
    Task<ChatResponse> ChatLLMAsync(string prompt, params string[] models);
}