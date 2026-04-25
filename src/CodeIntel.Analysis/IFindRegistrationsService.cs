using CodeIntel.Contracts;

namespace CodeIntel.Analysis;

/// <summary>
/// Выполняет поиск явных DI-регистраций типа в пределах решения.
/// </summary>
public interface IFindRegistrationsService
{
    /// <summary>
    /// Ищет явные регистрации символа с указанным именем в решении.
    /// </summary>
    /// <param name="solutionPath">Путь к файлу решения.</param>
    /// <param name="symbol">Имя типа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат поиска объявления и регистраций.</returns>
    Task<FindRegistrationsResponseDto> FindAsync(
        string solutionPath,
        string symbol,
        CancellationToken cancellationToken = default);
}
