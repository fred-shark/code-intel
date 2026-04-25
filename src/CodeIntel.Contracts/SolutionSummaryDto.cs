namespace CodeIntel.Contracts;

/// <summary>
/// Содержит сводную информацию по загруженному решению.
/// </summary>
public sealed record SolutionSummaryDto
{
    /// <summary>
    /// Возвращает абсолютный путь к файлу решения.
    /// </summary>
    public required string SolutionPath { get; init; }

    /// <summary>
    /// Возвращает список проектов, найденных в решении.
    /// </summary>
    public required IReadOnlyList<ProjectSummaryDto> Projects { get; init; }
}
