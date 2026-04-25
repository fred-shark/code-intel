using System.Collections.Generic;

namespace CodeIntel.Contracts;

/// <summary>
/// Возвращает структуру ответа для команды find-references.
/// </summary>
public sealed record FindReferencesResponseDto
{
    /// <summary>Путь к загруженному решению.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Запрошенное имя символа.</summary>
    public required string Symbol { get; init; }

    /// <summary>Объявление найденного символа или <see langword="null"/>, если символ не найден.</summary>
    public required FindReferencesDeclarationDto? Declaration { get; init; }

    /// <summary>Список найденных ссылок на символ.</summary>
    public required IReadOnlyList<FindReferencesResultDto> References { get; init; }

    /// <summary>Количество найденных ссылок.</summary>
    public required int ReferenceCount { get; init; }
}
