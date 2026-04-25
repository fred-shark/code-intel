using System.Collections.Generic;

namespace CodeIntel.Contracts;

/// <summary>
/// Возвращает структуру ответа для команды find-implementations.
/// </summary>
public sealed record FindImplementationsResponseDto
{
    /// <summary>Путь к загруженному решению.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Запрошенное имя символа.</summary>
    public required string Symbol { get; init; }

    /// <summary>Объявление найденного символа или <see langword="null"/>, если символ не найден.</summary>
    public required FindImplementationsDeclarationDto? Declaration { get; init; }

    /// <summary>Список найденных реализаций или наследников.</summary>
    public required IReadOnlyList<FindImplementationsResultDto> Implementations { get; init; }

    /// <summary>Количество найденных реализаций.</summary>
    public required int ImplementationCount { get; init; }
}
