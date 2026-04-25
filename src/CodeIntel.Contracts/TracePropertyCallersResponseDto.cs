using System.Collections.Generic;

namespace CodeIntel.Contracts;

/// <summary>
/// Возвращает структуру ответа для команды trace-property-callers.
/// </summary>
public sealed record TracePropertyCallersResponseDto
{
    /// <summary>Путь к загруженному решению.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Запрошенное имя типа.</summary>
    public required string Symbol { get; init; }

    /// <summary>Запрошенное имя свойства.</summary>
    public required string Property { get; init; }

    /// <summary>Запрошенный режим трассировки доступа к свойству.</summary>
    public required PropertyAccessKindDto Access { get; init; }

    /// <summary>Объявление найденного свойства или <see langword="null"/>, если тип или свойство не найдены.</summary>
    public required TracePropertyCallersDeclarationDto? Declaration { get; init; }

    /// <summary>Набор деревьев вызовов по режимам доступа к свойству.</summary>
    public required IReadOnlyList<PropertyAccessCallChainsDto> AccessChains { get; init; }
}
