using CodeIntel.Contracts;

namespace CodeIntel.Analysis;

/// <summary>
/// Выполняет базовый анализ влияния изменений для типа в пределах решения.
/// </summary>
public interface IAnalyzeImpactService
{
    /// <summary>
    /// Анализирует влияние изменений для символа с указанным именем в решении.
    /// </summary>
    /// <param name="solutionPath">Путь к файлу решения.</param>
    /// <param name="symbol">Имя типа.</param>
    /// <param name="includeTests"><see langword="true"/>, если нужно учитывать реализации из тестовых проектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Агрегированный результат анализа влияния изменений.</returns>
    Task<AnalyzeImpactResponseDto> AnalyzeAsync(
        string solutionPath,
        string symbol,
        bool includeTests = false,
        CancellationToken cancellationToken = default);
}
