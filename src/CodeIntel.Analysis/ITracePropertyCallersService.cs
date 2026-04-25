using CodeIntel.Contracts;

namespace CodeIntel.Analysis;

/// <summary>
/// Трассирует цепочки вызовов для чтений и записей свойства вверх до точек входа в пределах решения.
/// </summary>
public interface ITracePropertyCallersService
{
    /// <summary>
    /// Строит дерево вызовов для указанного свойства типа.
    /// </summary>
    /// <param name="solutionPath">Путь к файлу решения.</param>
    /// <param name="symbol">Имя типа, содержащего свойство.</param>
    /// <param name="property">Имя свойства.</param>
    /// <param name="access">Режим трассировки доступа к свойству.</param>
    /// <param name="maxDepth">Максимальная глубина рекурсивного подъёма по вызовам. По умолчанию 15.</param>
    /// <param name="includeTests">Включать ли вызовы из тестовых проектов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Деревья вызовов с точками входа и условиями для выбранных режимов доступа.</returns>
    Task<TracePropertyCallersResponseDto> FindAsync(
        string solutionPath,
        string symbol,
        string property,
        PropertyAccessKindDto access = PropertyAccessKindDto.Both,
        int maxDepth = 15,
        bool includeTests = false,
        CancellationToken cancellationToken = default);
}
