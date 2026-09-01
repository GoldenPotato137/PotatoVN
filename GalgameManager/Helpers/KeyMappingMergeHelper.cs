using GalgameManager.Models;
using Windows.System;

namespace GalgameManager.Helpers;

/// <summary>
/// 合并全局按键映射与游戏专属映射，并兼容旧版保存过的全局映射快照。
/// </summary>
public static class KeyMappingMergeHelper
{
    /// <summary>
    /// 生成游戏设置界面与启动任务共用的有效映射列表。
    /// 游戏专属的同来源规则优先；关闭的继承规则由一个不含目标键的标记持久化。
    /// </summary>
    public static List<KeyMapping> BuildEffectiveMappings(
        IEnumerable<KeyMapping>? storedGameMappings,
        IEnumerable<KeyMapping>? globalMappings)
    {
        List<KeyMapping> globals = (globalMappings ?? [])
            .Where(HasSource)
            .Select(Clone)
            .ToList();
        List<KeyMapping> localMappings = [];
        List<List<int>> suppressedGlobalSources = [];

        foreach (KeyMapping stored in storedGameMappings ?? [])
        {
            if (!HasSource(stored))
            {
                if (!stored.IsGlobal)
                    localMappings.Add(CloneAsLocal(stored));
                continue;
            }

            if (!stored.IsGlobal)
            {
                localMappings.Add(CloneAsLocal(stored));
                continue;
            }

            // 新版的游戏级“停用继承规则”标记：只有来源键，没有目标键。
            if (!HasTarget(stored))
            {
                if (!stored.IsEnabled)
                    AddSourceIfMissing(suppressedGlobalSources, stored.From);
                continue;
            }

            // 旧版会把继承的全局规则完整复制进游戏存档。若内容仍与当前
            // 全局规则一致，则视为旧快照；若目标键已经改过，则迁移为游戏专属覆盖。
            KeyMapping? matchingGlobal = globals.FirstOrDefault(global => SameSource(global, stored));
            if (matchingGlobal is not null && SameTarget(matchingGlobal, stored))
            {
                if (!stored.IsEnabled)
                    AddSourceIfMissing(suppressedGlobalSources, stored.From);
                continue;
            }

            localMappings.Add(CloneAsLocal(stored));
        }

        List<KeyMapping> result = [];
        List<List<int>> addedGlobalSources = [];
        foreach (KeyMapping global in globals)
        {
            // 来源键和目标键齐全、且总规则自身启用时，才是一条可执行的全局规则。
            // 旧版只有来源键的配置会保留在设置页，等待用户补齐目标键。
            if (!global.IsEnabled || !HasTarget(global) ||
                ContainsSource(addedGlobalSources, global.From) ||
                localMappings.Any(local => SourcesOverlap(local.From, global.From)))
                continue;

            KeyMapping inherited = Clone(global);
            inherited.IsGlobal = true;
            inherited.IsEnabled = !ContainsSource(suppressedGlobalSources, global.From);
            result.Add(inherited);
            AddSourceIfMissing(addedGlobalSources, global.From);
        }

        result.AddRange(localMappings);
        return result;
    }

    /// <summary>
    /// 将游戏编辑器中的合并列表还原为游戏自身需要保存的内容。
    /// 正常启用的全局规则不再复制进每个游戏，只保存本地规则与游戏级停用标记。
    /// </summary>
    public static List<KeyMapping> BuildPersistedGameMappings(IEnumerable<KeyMapping>? editorMappings)
    {
        List<KeyMapping> result = [];
        foreach (KeyMapping mapping in editorMappings ?? [])
        {
            if (!mapping.IsGlobal)
            {
                result.Add(CloneAsLocal(mapping));
                continue;
            }

            if (!mapping.IsEnabled && HasSource(mapping))
            {
                result.Add(new KeyMapping
                {
                    From = [.. mapping.From],
                    To = [],
                    Remark = mapping.Remark,
                    IsEnabled = false,
                    IsGlobal = true,
                });
            }
        }

        return result;
    }

    public static KeyMapping Clone(KeyMapping mapping) => new()
    {
        From = mapping.From is null ? [] : [.. mapping.From],
        To = mapping.To is null ? [] : [.. mapping.To],
        Remark = mapping.Remark ?? string.Empty,
        IsEnabled = mapping.IsEnabled,
        IsGlobal = mapping.IsGlobal,
    };

    private static KeyMapping CloneAsLocal(KeyMapping mapping)
    {
        KeyMapping clone = Clone(mapping);
        clone.IsGlobal = false;
        return clone;
    }

    private static bool HasSource(KeyMapping mapping) => mapping.From is { Count: > 0 };

    private static bool HasTarget(KeyMapping mapping) => mapping.To is { Count: > 0 };

    private static bool SameSource(KeyMapping left, KeyMapping right) =>
        left.From is not null && right.From is not null && left.From.SequenceEqual(right.From);

    private static bool SameTarget(KeyMapping left, KeyMapping right) =>
        left.To is not null && right.To is not null && left.To.SequenceEqual(right.To);

    /// <summary>
    /// Returns true when two source definitions can match the same physical input.
    /// Generic Ctrl/Shift/Alt entries therefore overlap their left and right variants.
    /// </summary>
    public static bool SourcesOverlap(IReadOnlyList<int>? left, IReadOnlyList<int>? right)
    {
        if (left is not { Count: > 0 } || right is not { Count: > 0 }) return false;
        HashSet<string> leftSignatures = ExpandSourceSignatures(left);
        return ExpandSourceSignatures(right).Overlaps(leftSignatures);
    }

    /// <summary>
    /// Returns true when two source definitions describe the same physical inputs.
    /// Key order is ignored, while a generic modifier remains different from one
    /// explicitly selected left/right modifier.
    /// </summary>
    public static bool SourcesEquivalent(IReadOnlyList<int>? left, IReadOnlyList<int>? right)
    {
        if (left is not { Count: > 0 } || right is not { Count: > 0 }) return false;
        return ExpandSourceSignatures(left).SetEquals(ExpandSourceSignatures(right));
    }

    public static bool HasDuplicateEnabledSources(IEnumerable<KeyMapping> mappings)
    {
        List<KeyMapping> effective = mappings
            .Where(mapping => mapping.IsEnabled && HasSource(mapping) && HasTarget(mapping))
            .ToList();
        for (var index = 0; index < effective.Count; index++)
        for (var other = index + 1; other < effective.Count; other++)
            if (SourcesOverlap(effective[index].From, effective[other].From))
                return true;
        return false;
    }

    public static HashSet<string> ExpandSourceSignatures(IReadOnlyList<int> source)
    {
        List<List<int>> combinations = [[]];
        foreach (int rawKey in source)
        {
            int[] variants = (VirtualKey)rawKey switch
            {
                VirtualKey.Control => [(int)VirtualKey.LeftControl, (int)VirtualKey.RightControl],
                VirtualKey.Shift => [(int)VirtualKey.LeftShift, (int)VirtualKey.RightShift],
                VirtualKey.Menu => [(int)VirtualKey.LeftMenu, (int)VirtualKey.RightMenu],
                VirtualKey.RightWindows => [(int)VirtualKey.LeftWindows],
                _ => [rawKey],
            };

            List<List<int>> expanded = [];
            foreach (List<int> combination in combinations)
            foreach (int variant in variants)
                expanded.Add([.. combination, variant]);
            combinations = expanded;
        }

        return combinations
            .Select(CreateSourceSignature)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static string CreateSourceSignature(IEnumerable<int> source) =>
        string.Join(',', source.Distinct().Order());

    /// <summary>
    /// Builds the keyboard-key candidates for each mouse source button. Keeping the
    /// candidates per button prevents a chord for one mouse button from changing how
    /// an unrelated button is matched.
    /// </summary>
    public static Dictionary<int, HashSet<int>> BuildMouseSourceKeyboardKeyIndex(
        IEnumerable<KeyMapping> mappings)
    {
        Dictionary<int, HashSet<int>> result = [];
        foreach (KeyMapping mapping in mappings.Where(mapping =>
                     mapping.IsEnabled && HasSource(mapping) && HasTarget(mapping)))
        foreach (string signature in ExpandSourceSignatures(mapping.From))
        {
            int[] keys = signature.Split(',').Select(int.Parse).ToArray();
            int[] mouseButtons = keys.Where(IsMouseButtonCode).ToArray();
            if (mouseButtons.Length == 0) continue;

            int[] keyboardKeys = keys.Where(key => !IsMouseButtonCode(key)).ToArray();
            foreach (int mouseButton in mouseButtons)
            {
                if (!result.TryGetValue(mouseButton, out HashSet<int>? candidates))
                    result[mouseButton] = candidates = [];
                candidates.UnionWith(keyboardKeys);
            }
        }

        return result;
    }

    /// <summary>
    /// 将按键映射使用的鼠标按钮编号转换为 GetAsyncKeyState 所需的虚拟键码。
    /// </summary>
    public static int GetMouseButtonVirtualKey(int mouseButton) => mouseButton switch
    {
        1 => 0x01,
        2 => 0x02,
        3 => 0x04,
        4 => 0x05,
        5 => 0x06,
        _ => 0,
    };

    private static bool IsMouseButtonCode(int key) => key is >= 1 and <= 7;

    private static bool ContainsSource(IEnumerable<List<int>> sources, IEnumerable<int> source) =>
        sources.Any(item => item.SequenceEqual(source));

    private static void AddSourceIfMissing(ICollection<List<int>> sources, IEnumerable<int> source)
    {
        if (!ContainsSource(sources, source))
            sources.Add([.. source]);
    }
}
