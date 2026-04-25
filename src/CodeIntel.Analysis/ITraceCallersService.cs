using CodeIntel.Contracts;

namespace CodeIntel.Analysis;

/// <summary>
/// Трассирует цепочку вызовов метода вверх до точек входа в пределах решения.
/// </summary>
public interface ITraceCallersService
{
    /// <summary>
    /// Строит дерево вызовов для указанного метода типа.
    /// </summary>
    /// <param name="solutionPath">Путь к файлу решения.</param>
    /// <param name="symbol">Имя типа, содержащего метод.</param>
    /// <param name="method">Имя метода.</param>
    /// <param name="maxDepth">Максимальная глубина рекурсивного подъёма по вызовам. По умолчанию 15.</param>
    /// <param name="includeTests">Включать ли вызовы из тестовых проектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Дерево вызовов с точками входа и условиями.</returns>
    Task<TraceCallersResponseDto> FindAsync(
        string solutionPath,
        string symbol,
        string method,
        int maxDepth = 15,
        bool includeTests = false,
        CancellationToken cancellationToken = default);
}
