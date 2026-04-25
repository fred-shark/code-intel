using CodeIntel.Contracts;

namespace CodeIntel.Analysis;

/// <summary>
/// Выполняет семантический поиск символов внутри решения.
/// </summary>
public interface IFindSymbolService
{
    /// <summary>
    /// Ищет символы с указанным именем в решении.
    /// </summary>
    /// <param name="solutionPath">Путь к файлу решения.</param>
    /// <param name="name">Имя символа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результаты поиска.</returns>
    Task<FindSymbolResponseDto> FindAsync(string solutionPath, string name, CancellationToken cancellationToken = default);
}
