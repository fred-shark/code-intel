namespace CodeIntel.Contracts;

/// <summary>
/// Описывает объявление свойства, для которого выполняется трассировка чтений и записей.
/// </summary>
public sealed record TracePropertyCallersDeclarationDto
{
    /// <summary>Имя типа, содержащего свойство.</summary>
    public required string Symbol { get; init; }

    /// <summary>Имя свойства.</summary>
    public required string Property { get; init; }

    /// <summary>Полностью квалифицированное имя содержащего типа.</summary>
    public required string? FullyQualifiedTypeName { get; init; }

    /// <summary>Вид типа, содержащего свойство.</summary>
    public required SymbolKindDto Kind { get; init; }

    /// <summary>Название проекта, в котором объявлено свойство.</summary>
    public required string Project { get; init; }

    /// <summary>Абсолютный путь к файлу с объявлением свойства.</summary>
    public required string FilePath { get; init; }

    /// <summary>Номер строки объявления свойства (1-индексированный).</summary>
    public required int Line { get; init; }

    /// <summary>Номер столбца объявления свойства (1-индексированный).</summary>
    public required int Column { get; init; }

    /// <summary>Признак наличия getter-аксессора.</summary>
    public required bool HasGetter { get; init; }

    /// <summary>Признак наличия setter-аксессора.</summary>
    public required bool HasSetter { get; init; }
}
