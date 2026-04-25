using System.Collections.Generic;

namespace CodeIntel.Contracts;

/// <summary>
/// Возвращает структуру ответа для команды analyze-impact.
/// </summary>
public sealed record AnalyzeImpactResponseDto
{
    /// <summary>Путь к загруженному решению.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Запрошенное имя символа.</summary>
    public required string Symbol { get; init; }

    /// <summary>Объявление найденного символа или <see langword="null"/>, если символ не найден.</summary>
    public required AnalyzeImpactDeclarationDto? Declaration { get; init; }

    /// <summary>Количество найденных ссылок на символ.</summary>
    public required int ReferenceCount { get; init; }

    /// <summary>Количество найденных реализаций или наследников.</summary>
    public required int ImplementationCount { get; init; }

    /// <summary>Упорядоченный список затронутых проектов.</summary>
    public required IReadOnlyList<string> AffectedProjects { get; init; }

    /// <summary>Детерминированная сводка уровня риска для MVP.</summary>
    public required string RiskSummary { get; init; }
}
