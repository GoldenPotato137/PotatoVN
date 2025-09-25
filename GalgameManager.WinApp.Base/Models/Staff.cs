using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Enums;
using LiteDB;
using Newtonsoft.Json;

namespace GalgameManager.Models;

public partial class Staff : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string?[] Ids { get; set; } = new string[Galgame.PhraserNumber];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Name))] private string? _japaneseName;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Name))] private string? _englishName;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Name))] private string? _chineseName;
    [ObservableProperty] private Gender _gender;
    public ObservableCollection<Career> Career { get; set; } = [];
    [ObservableProperty] private string? _imagePath;
    /// 图片下载地址（来自搜刮源），若手动设置了图片，这个会被置为null（以确保pvn服务同步时使用手动设置的图片）
    public string? ImageUrl { get; set; }
    [ObservableProperty] private string? _description;
    [ObservableProperty] private DateTime? _birthDate;
    public ObservableCollection<StaffGame> Games { get; set; } = [];
    public bool RequirePvnSync;
    
    public string? Name => JapaneseName ?? ChineseName ?? EnglishName;

    public override string ToString() => Name ?? "Unknown";

    public StaffIdentifier GetIdentifier() => new()
    {
        Ids = Ids,
        JapaneseName = JapaneseName,
        EnglishName = EnglishName,
        ChineseName = ChineseName
    };

    /// 如果已经存在则更新relation
    /// <param name="game">游戏</param>
    /// <param name="relation">在该游戏中担当的职位</param>
    public void AddGame(Galgame game, List<Career> relation)
    {
        StaffGame? tmp = Games.FirstOrDefault(g => g.GameId == game.Uuid);
        if (tmp is not null)
        {
            tmp.Relation = relation;
            return;
        }
        Games.Add(new StaffGame {Game = game, Relation = relation});
    }

    /// <summary>
    /// 移除staff的某个作品，如果不存在则不做任何操作
    /// </summary>
    /// <param name="game"></param>
    public void RemoveGame(Galgame game)
    {
        List<StaffGame> tmp = Games.Where(g => g.GameId == game.Uuid).ToList();
        foreach (StaffGame staffGame in tmp) Games.Remove(staffGame);
    }

    /// <summary>
    /// 若game不属于staff的任何一个作品，则返回null
    /// </summary>
    public List<Career>? GetRelation(Galgame game) =>
        Games.FirstOrDefault(g => g.GameId == game.Uuid)?.Relation;
}

public class StaffGame
{
    [BsonIgnore][JsonIgnore] public Galgame Game { get; set; } = null!;
    public List<Career> Relation { get; set; } = [];

    #region LiteDB

    public Guid GameId
    {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        get => Game?.Uuid ?? LoadedGameId; //导出到Json时是直接读数据库的，不会去加载Game，直接输出LoadedGameId
        set => LoadedGameId = value;
    }

    [BsonIgnore][JsonIgnore] public Guid LoadedGameId { get; private set; } = Guid.Empty;

    #endregion
}

public class StaffRelation : Staff
{
    public List<Career> Relation { get; set; } = [];
}

public class StaffIdentifier
{
    public string?[] Ids { get; set; } = new string[Galgame.PhraserNumber];
    public string? JapaneseName { get; set; }
    public string? EnglishName { get; set; }
    public string? ChineseName { get; set; }

    public int Match(Staff staff)
    {
        var score = 0;
        score += JapaneseName is not null && staff.JapaneseName == JapaneseName ? 1 : 0;
        score += EnglishName is not null && staff.EnglishName == EnglishName ? 1 : 0;
        score += ChineseName is not null && staff.ChineseName == ChineseName ? 1 : 0;
        for (var i = 0; i < Galgame.PhraserNumber; i++)
            score += Ids[i] is not null && staff.Ids[i] == Ids[i] ? 1 : 0;
        return score;
    }

    public StaffIdentifier SetId(RssType rss, string? id)
    {
        Ids[(int)rss] = id;
        return this;
    }
}