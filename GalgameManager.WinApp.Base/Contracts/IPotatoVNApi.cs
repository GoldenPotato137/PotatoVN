using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.LLM;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.WinApp.Base.Contracts;

public interface IPotatoVnApi
{
    //与游戏相关的API
    #region GAMES

    /// <summary>
    /// 获取所有游戏，这个列表只是一个快照（即后续添加的游戏或删除的游戏均不会在这个List中反馈）
    /// </summary>
    /// <returns></returns>
    public List<Galgame> GetAllGames();

    #endregion

    //与插件数据存储相关的API
    #region DATA

    /// <summary>
    /// 读取本插件存储的数据
    /// </summary>
    /// <returns></returns>
    public Task<string?> GetDataAsync();

    /// <summary>
    /// 保存本插件存储的数据
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public Task SaveDataAsync(string data);

    #endregion

    //与消息相关的API
    #region MESSAGES

    /// <summary>
    /// 全局消息信使
    /// </summary>
    public IMessenger Messenger { get; }

    #endregion

    //与事件/通知相关的API
    #region NOTIFICATIONS 

    /// <summary>
    /// 使用InfoBar通知信息，若title与msg均为空则关闭InfoBar
    /// </summary>
    /// <param name="infoBarSeverity"></param>
    /// <param name="title"></param>
    /// <param name="msg"></param>
    /// <param name="displayTimeMs"></param>
    public void Info(InfoBarSeverity infoBarSeverity, string? title = null, string? msg = null, int? displayTimeMs = 3000);

    /// <summary>
    /// 记录并通知事件
    /// </summary>
    /// <param name="infoBarSeverity">严重程度</param>
    /// <param name="title">事件名</param>
    /// <param name="exception">与之相关的异常，若不是异常则不填</param>
    /// <param name="msg">事件信息</param>
    /// <param name="callbackAction">点击按钮后执行的回调</param>
    /// <param name="callbackButtonText">按钮上显示的文字</param>
    public void Event(InfoBarSeverity infoBarSeverity, string title, Exception? exception = null, string? msg = null,
        Action? callbackAction = null, string? callbackButtonText = null);

    /// <summary>
    /// 不严重的非预期错误，仅在开发模式下通知
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="infoBarSeverity"></param>
    /// <param name="e"></param>
    public void DeveloperEvent(InfoBarSeverity infoBarSeverity = InfoBarSeverity.Warning, string? msg = null,
        Exception? e = null);

    /// <summary>
    /// 手动记录日志，默认只将severity >= InfoBarSeverity.Warning的日志通知，开发者模式下通知所有日志 <br/>
    /// <see cref="Event"/>会自动调用该方法记录日志
    /// </summary>
    /// <param name="severity"></param>
    /// <param name="msg"></param>
    public void Log(InfoBarSeverity severity = InfoBarSeverity.Warning, string msg = "");

    #endregion

    //与后台任务相关的API
    #region BG_TASKS

    /// <summary>
    /// 新增后台任务
    /// </summary>
    /// <returns>这个后台任务对应的task</returns>
    public Task AddBgTask(BgTaskBase bgTask);

    /// <summary>
    /// 获取所有后台任务
    /// </summary>
    public IEnumerable<BgTaskBase> GetBgTasks();

    /// <summary>
    /// 获取指定类型的后台任务，如果没有则返回null
    /// </summary>
    /// <param name="key">关键字</param>
    public T? GetBgTask<T>(string key) where T : BgTaskBase;

    #endregion BG_TASKS


    #region UTILS

    /// <summary>
    /// 下载图片并保存到本地
    /// </summary>
    /// <param name="imageUrl">图片链接</param>
    /// <param name="imageName">图片名，<b>不用后缀名</b></param>
    /// <param name="client">自定义httpClient，若不指定则使用potatovn的默认HttpClient</param>
    /// <param name="onException">异常回调</param>
    /// <returns>下载后图片路径，若下载失败返回null</returns>
    public Task<string?> DownloadImageAsync(string imageUrl, string imageName, HttpClient? client,
        Action<Exception>? onException = null);

    /// <summary>
    /// 获取插件所在路径
    /// </summary>
    /// <returns></returns>
    public string GetPluginPath();

    /// <summary>
    /// 在主线程执行某个操作（一般用于UI相关操作）
    /// </summary>
    /// <param name="action"></param>
    public void InvokeOnMainThread(Action action);

    #endregion

    #region LLM

    /// <summary>
    /// 与LLM进行对话
    /// </summary>
    /// <param name="prompt">用户提问</param>
    /// <param name="models">可选的模型名称数组，如果传入则批量调用匹配的模型，不传则使用默认模型</param>
    /// <returns>单个AI响应或批量响应</returns>
    Task<ChatResponse> ChatLLMAsync(string prompt, params string[] models);

    #endregion
}
