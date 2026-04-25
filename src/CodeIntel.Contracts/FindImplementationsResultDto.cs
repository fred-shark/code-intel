namespace CodeIntel.Contracts;

/// <summary>
/// Представляет одну найденную реализацию типа в коде решения.
/// </summary>
public sealed record FindImplementationsResultDto
{
    /// <summary>Имя найденной реализации.</summary>
    public required string Symbol { get; init; }

    /// <summary>Полностью квалифицированное имя реализации, если его удалось вычислить.</summary>
    public required string? FullyQualifiedName { get; init; }

    /// <summary>Вид найденного типа.</summary>
    public required SymbolKindDto Kind { get; init; }

    /// <summary>Название проекта, содержащего реализацию.</summary>
    public required string Project { get; init; }

    /// <summary>Абсолютный путь к файлу, содержащему реализацию.</summary>
    public required string FilePath { get; init; }

    /// <summary>Номер строки реализации (1-индексированный).</summary>
    public required int Line { get; init; }

    /// <summary>Номер столбца реализации (1-индексированный).</summary>
    public required int Column { get; init; }
}
