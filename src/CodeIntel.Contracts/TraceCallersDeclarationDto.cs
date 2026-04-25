namespace CodeIntel.Contracts;

/// <summary>
/// Описывает объявление метода, для которого выполняется трассировка вызовов.
/// </summary>
public sealed record TraceCallersDeclarationDto
{
    /// <summary>Имя типа, содержащего метод.</summary>
    public required string Symbol { get; init; }

    /// <summary>Имя метода.</summary>
    public required string Method { get; init; }

    /// <summary>Полностью квалифицированное имя содержащего типа.</summary>
    public required string? FullyQualifiedTypeName { get; init; }

    /// <summary>Вид типа, содержащего метод.</summary>
    public required SymbolKindDto Kind { get; init; }

    /// <summary>Название проекта, в котором объявлен метод.</summary>
    public required string Project { get; init; }

    /// <summary>Абсолютный путь к файлу с объявлением метода.</summary>
    public required string FilePath { get; init; }

    /// <summary>Номер строки объявления метода (1-индексированный).</summary>
    public required int Line { get; init; }

    /// <summary>Номер столбца объявления метода (1-индексированный).</summary>
    public required int Column { get; init; }
}
