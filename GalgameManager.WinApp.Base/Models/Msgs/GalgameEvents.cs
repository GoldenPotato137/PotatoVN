using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging.Messages;
using GalgameManager.Models;

namespace GalgameManager.WinApp.Base.Models.Msgs;

public class GalgamePlayedMessage(Galgame galgame) : ValueChangedMessage<Galgame>(galgame);

/// <summary>
/// 仅在启动弹窗判断完成、原生计时可以创建游玩时段后发送。
/// 需要正式游玩生命周期的插件应优先监听此消息，而不是 <see cref="GalgamePlayedMessage"/>。
/// </summary>
public class GalgamePlayTimeRecordingStartedMessage(Galgame galgame) : ValueChangedMessage<Galgame>(galgame);

/// <summary>
/// 将游戏已保存映射状态的不可变副本传递给正在运行的按键映射任务。
/// </summary>
public class KeyMappingsChangedMessage : AsyncRequestMessage<bool>
{
    public Guid GalgameUuid { get; }
    public IReadOnlyList<KeyMapping> GameMappings { get; }
    public bool GameMappingOptInEnabled { get; }

    public KeyMappingsChangedMessage(Galgame galgame)
    {
        GalgameUuid = galgame.Uuid;
        GameMappings = (galgame.KeyMappings ?? [])
            .Select(Clone)
            .ToArray();
        GameMappingOptInEnabled = galgame.KeyReMap;
    }

    private static KeyMapping Clone(KeyMapping mapping) => new()
    {
        From = mapping.From is null ? [] : [.. mapping.From],
        To = mapping.To is null ? [] : [.. mapping.To],
        Remark = mapping.Remark ?? string.Empty,
        IsEnabled = mapping.IsEnabled,
        IsGlobal = mapping.IsGlobal,
    };
}

public class GalgameStoppedMessage(Galgame galgame) : ValueChangedMessage<Galgame>(galgame);
