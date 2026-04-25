using CodeIntel.Contracts;

namespace CodeIntel.Analysis;

/// <summary>
/// Выполняет семантический поиск реализаций интерфейсов и абстрактных классов в пределах решения.
/// </summary>
public interface IFindImplementationsService
{
    /// <summary>
    /// Ищет реализации символа с указанным именем в решении.
    /// </summary>
    /// <param name="solutionPath">Путь к файлу решения.</param>
    /// <param name="symbol">Имя типа.</param>
    /// <param name="includeTests"><see langword="true"/>, если нужно включить результаты из тестовых проектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат поиска объявления и реализаций.</returns>
    Task<FindImplementationsResponseDto> FindAsync(
        string solutionPath,
        string symbol,
        bool includeTests = false,
        CancellationToken cancellationToken = default);
}
