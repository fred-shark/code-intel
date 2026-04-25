using CodeIntel.Contracts;

namespace CodeIntel.Loader;

/// <summary>
/// Определяет контракт загрузки краткой сводки по решению.
/// </summary>
public interface ISolutionSummaryLoader
{
    /// <summary>
    /// Загружает файл решения и возвращает список проектов с основной метаинформацией.
    /// </summary>
    /// <param name="solutionPath">Путь к файлу решения `.sln` или `.slnx`.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Сводка по решению.</returns>
    Task<SolutionSummaryDto> LoadAsync(string solutionPath, CancellationToken cancellationToken = default);
}
