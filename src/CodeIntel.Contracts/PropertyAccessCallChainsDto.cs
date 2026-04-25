using System.Collections.Generic;

namespace CodeIntel.Contracts;

/// <summary>
/// Содержит дерево вызовов для конкретного режима доступа к свойству.
/// </summary>
public sealed record PropertyAccessCallChainsDto
{
    /// <summary>Режим доступа к свойству, для которого построены цепочки.</summary>
    public required PropertyAccessKindDto Access { get; init; }

    /// <summary>Дерево вызовов от методов, где свойство читается или записывается, до точек входа.</summary>
    public required IReadOnlyList<CallChainNodeDto> CallChains { get; init; }

    /// <summary>Количество уникальных точек входа в дереве вызовов.</summary>
    public required int EntryPointCount { get; init; }
}
