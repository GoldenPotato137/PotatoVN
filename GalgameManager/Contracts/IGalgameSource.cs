﻿using System.Collections.ObjectModel;
using GalgameManager.Enums;
using GalgameManager.Models;
using Newtonsoft.Json;
using StdPath = System.IO.Path;


namespace GalgameManager.Contracts;

public interface IGalgameSource
{
    public SourceType GetSourceType();
    public string Url { get; set; }
    public Task<ObservableCollection<Galgame>> GetGalgameList();
    public Galgame GetGalgameByName(string name);
    
    [JsonIgnore] public bool IsRunning { get; set; }
    [JsonIgnore] public bool IsUnpacking { get; set; }
    
    public bool ScanOnStart { get; set; }

    /// <summary>
    /// 向库中新增一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    public void AddGalgame(Galgame galgame);

    /// <summary>
    /// 从库中删除一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    public void DeleteGalgame(Galgame galgame);

    public bool IsInSource(Galgame galgame);

    /// <summary>
    /// 检查这个路径的游戏是否应该这个库中
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns></returns>
    public bool IsInSource(string path);
    
    /// <summary>
    /// 获取这个库的日志路径（相对存储根目录）
    /// </summary>
    public string GetLogPath();
    
    public string GetLogName();
}