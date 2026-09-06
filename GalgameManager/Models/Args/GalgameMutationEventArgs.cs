using GalgameManager.Enums;

namespace GalgameManager.Models;

public sealed class GalgameMutationEventArgs(
    Galgame game,
    GalgameChangeKind changes,
    GalgameChangeOrigin origin,
    GameParseType parsedTypes = GameParseType.None) : EventArgs
{
    public Galgame Game { get; } = game;
    public GalgameChangeKind Changes { get; } = changes;
    public GalgameChangeOrigin Origin { get; } = origin;
    public GameParseType ParsedTypes { get; } = parsedTypes;
}
