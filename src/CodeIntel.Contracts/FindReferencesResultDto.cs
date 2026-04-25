namespace CodeIntel.Contracts;

/// <summary>
/// Представляет одну найденную ссылку на тип в коде решения.
/// </summary>
public sealed record FindReferencesResultDto
{
    /// <summary>Имя символа, на который указывает ссылка.</summary>
    public required string Symbol { get; init; }

    /// <summary>Полностью квалифицированное имя символа назначения, если его удалось вычислить.</summary>
    public required string? ReferencedSymbolFullyQualifiedName { get; init; }

    /// <summary>Название проекта, содержащего ссылку.</summary>
    public required string Project { get; init; }

    /// <summary>Абсолютный путь к файлу, содержащему ссылку.</summary>
    public required string FilePath { get; init; }

    /// <summary>Номер строки ссылки (1-индексированный).</summary>
    public required int Line { get; init; }

    /// <summary>Номер столбца ссылки (1-индексированный).</summary>
    public required int Column { get; init; }
}
