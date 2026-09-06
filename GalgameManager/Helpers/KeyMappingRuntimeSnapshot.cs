using System.Collections.Frozen;
using GalgameManager.Models;

namespace GalgameManager.Helpers;

/// <summary>
/// 构建完成后不再修改的运行时映射快照，供低级钩子回调无锁读取。
/// </summary>
public sealed class KeyMappingRuntimeSnapshot
{
    public static KeyMappingRuntimeSnapshot Empty { get; } = new([], []);

    public FrozenDictionary<string, KeyMappingOutput> LookupMap { get; }
    public FrozenDictionary<int, FrozenSet<int>> MouseSourceKeyboardKeysByButton { get; }
    public bool IsEmpty => LookupMap.Count == 0;

    private KeyMappingRuntimeSnapshot(
        Dictionary<string, KeyMappingOutput> lookupMap,
        Dictionary<int, FrozenSet<int>> mouseSourceKeyboardKeysByButton)
    {
        LookupMap = lookupMap.ToFrozenDictionary(StringComparer.Ordinal);
        MouseSourceKeyboardKeysByButton = mouseSourceKeyboardKeysByButton.ToFrozenDictionary();
    }

    public static KeyMappingRuntimeSnapshot Create(IEnumerable<KeyMapping>? mappings)
    {
        List<KeyMapping> clonedMappings = (mappings ?? [])
            .Select(KeyMappingMergeHelper.Clone)
            .ToList();
        Dictionary<string, KeyMappingOutput> lookupMap = new(StringComparer.Ordinal);

        // 有效规则已按“全局后被游戏专属覆盖”的优先级排好，保留第一个匹配项。
        foreach (KeyMapping mapping in clonedMappings)
        {
            if (!mapping.IsEnabled || mapping.From.Count == 0 || mapping.To.Count == 0) continue;
            KeyMappingOutput? output = KeyMappingOutputFactory.Create(mapping.To);
            if (output is null) continue;
            foreach (string signature in KeyMappingMergeHelper.ExpandSourceSignatures(mapping.From))
                lookupMap.TryAdd(signature, output);
        }

        Dictionary<int, FrozenSet<int>> mouseIndex = KeyMappingMergeHelper
            .BuildMouseSourceKeyboardKeyIndex(clonedMappings)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToFrozenSet());
        return lookupMap.Count == 0
            ? Empty
            : new KeyMappingRuntimeSnapshot(lookupMap, mouseIndex);
    }

    /// <summary>
    /// 根据两个独立开关生成真正应在游戏中生效的规则。
    /// </summary>
    public static List<KeyMapping> BuildEffectiveMappings(
        IEnumerable<KeyMapping>? gameMappings,
        IEnumerable<KeyMapping>? globalMappings,
        bool gameEnabled,
        bool globalEnabled)
    {
        // 保持现有开关语义：全局开关为所有游戏启用合并后的完整规则；
        // 全局开关关闭时，KeyReMap 可单独为当前游戏启用。
        if (!gameEnabled && !globalEnabled) return [];
        return KeyMappingMergeHelper.BuildEffectiveMappings(gameMappings, globalMappings);
    }
}
/// <summary>
/// 热更新时暂时屏蔽仍处于按下状态的物理来源，直到用户真实释放它们。
/// </summary>
public sealed class KeyMappingHeldInputGuard
{
    private readonly HashSet<int> _keyboard = [];
    private readonly HashSet<int> _mouse = [];

    public void BeginTransition(IEnumerable<int> pressedKeyboard, IEnumerable<int> pressedMouse)
    {
        _keyboard.UnionWith(pressedKeyboard);
        _mouse.UnionWith(pressedMouse);
    }

    public bool IsKeyboardSuppressed(IEnumerable<int> involvedKeys) => involvedKeys.Any(_keyboard.Contains);

    public bool IsMouseSuppressed(int mouseButton, IEnumerable<int> involvedKeyboardKeys) =>
        _mouse.Contains(mouseButton) || involvedKeyboardKeys.Any(_keyboard.Contains);

    public void ReleaseKeyboard(int key) => _keyboard.Remove(key);

    public void ReleaseMouse(int button) => _mouse.Remove(button);

    public void Clear()
    {
        _keyboard.Clear();
        _mouse.Clear();
    }
}
