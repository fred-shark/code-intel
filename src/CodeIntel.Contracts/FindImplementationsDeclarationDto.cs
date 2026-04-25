namespace CodeIntel.Contracts;

/// <summary>
/// Описывает объявление символа, для которого выполнялся поиск реализаций.
/// </summary>
public sealed record FindImplementationsDeclarationDto
{
    /// <summary>Имя объявленного символа.</summary>
    public required string Symbol { get; init; }

    /// <summary>Полностью квалифицированное имя символа, если его удалось вычислить.</summary>
    public required string? FullyQualifiedName { get; init; }

    /// <summary>Вид типа объявленного символа.</summary>
    public required SymbolKindDto Kind { get; init; }

    /// <summary>Название проекта, в котором расположен символ.</summary>
    public required string Project { get; init; }

    /// <summary>Абсолютный путь к файлу с объявлением символа.</summary>
    public required string FilePath { get; init; }

    /// <summary>Номер строки объявления символа (1-индексированный).</summary>
    public required int Line { get; init; }

    /// <summary>Номер столбца объявления символа (1-индексированный).</summary>
    public required int Column { get; init; }
}
