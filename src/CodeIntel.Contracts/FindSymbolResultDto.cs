namespace CodeIntel.Contracts;

/// <summary>
/// Представляет одну запись результата поиска символа.
/// </summary>
public sealed record FindSymbolResultDto
{
    /// <summary>Имя символа, совпавшее с поисковым запросом.</summary>
    public required string Name { get; init; }

    /// <summary>Полностью квалифицированное имя символа, если удалось его вычислить.</summary>
    public required string? FullyQualifiedName { get; init; }

    /// <summary>Тип символа.</summary>
    public required SymbolKindDto Kind { get; init; }

    /// <summary>Название проекта, к которому принадлежит символ.</summary>
    public required string Project { get; init; }

    /// <summary>Путь до файла, в котором объявлен символ.</summary>
    public required string FilePath { get; init; }

    /// <summary>Номер строки объявления символа (1-индексированный).</summary>
    public required int Line { get; init; }

    /// <summary>Номер столбца объявления символа (1-индексированный).</summary>
    public required int Column { get; init; }
}
