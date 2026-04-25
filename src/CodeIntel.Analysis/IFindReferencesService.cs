using CodeIntel.Contracts;

namespace CodeIntel.Analysis;

/// <summary>
/// Выполняет семантический поиск ссылок на тип в пределах решения.
/// </summary>
public interface IFindReferencesService
{
    /// <summary>
    /// Ищет ссылки на тип с указанным именем в решении.
    /// </summary>
    /// <param name="solutionPath">Путь к файлу решения.</param>
    /// <param name="symbol">Имя типа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат поиска объявления и ссылок на него.</returns>
    Task<FindReferencesResponseDto> FindAsync(string solutionPath, string symbol, CancellationToken cancellationToken = default);
}
