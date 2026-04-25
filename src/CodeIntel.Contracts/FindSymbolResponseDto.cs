using System.Collections.Generic;

namespace CodeIntel.Contracts;

/// <summary>
/// Возвращает структуру ответа для команды find-symbol.
/// </summary>
public sealed record FindSymbolResponseDto
{
    /// <summary>Путь к загруженному решению.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Запрошенное имя символа.</summary>
    public required string Name { get; init; }

    /// <summary>Список найденных совпадений.</summary>
    public required IReadOnlyList<FindSymbolResultDto> Results { get; init; }
}
