using System.Collections.Generic;

namespace CodeIntel.Contracts;

/// <summary>
/// Возвращает структуру ответа для команды trace-callers.
/// </summary>
public sealed record TraceCallersResponseDto
{
    /// <summary>Путь к загруженному решению.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Запрошенное имя типа.</summary>
    public required string Symbol { get; init; }

    /// <summary>Запрошенное имя метода.</summary>
    public required string Method { get; init; }

    /// <summary>Объявление найденного метода или <see langword="null"/>, если тип не найден.</summary>
    public required TraceCallersDeclarationDto? Declaration { get; init; }

    /// <summary>Дерево вызовов — прямые вызывающие целевого метода и их вызывающие рекурсивно до точек входа.</summary>
    public required IReadOnlyList<CallChainNodeDto> CallChains { get; init; }

    /// <summary>Количество уникальных точек входа в дереве вызовов.</summary>
    public required int EntryPointCount { get; init; }
}
