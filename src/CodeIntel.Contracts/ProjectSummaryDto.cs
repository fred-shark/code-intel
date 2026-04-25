namespace CodeIntel.Contracts;

/// <summary>
/// Описывает проект внутри решения.
/// </summary>
public sealed record ProjectSummaryDto
{
    /// <summary>
    /// Возвращает имя проекта.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Возвращает абсолютный путь к файлу проекта.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Возвращает список целевых платформ проекта.
    /// </summary>
    public required IReadOnlyList<string> TargetFrameworks { get; init; }

    /// <summary>
    /// Возвращает признак того, что проект классифицирован как тестовый.
    /// </summary>
    public required bool IsTestProject { get; init; }
}
