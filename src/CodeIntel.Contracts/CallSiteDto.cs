namespace CodeIntel.Contracts;

/// <summary>
/// Описывает место вызова метода внутри вызывающего кода.
/// </summary>
public sealed record CallSiteDto
{
    /// <summary>Абсолютный путь к файлу, содержащему вызов.</summary>
    public required string FilePath { get; init; }

    /// <summary>Номер строки вызова (1-индексированный).</summary>
    public required int Line { get; init; }

    /// <summary>Номер столбца вызова (1-индексированный).</summary>
    public required int Column { get; init; }

    /// <summary>Текст условия ближайшего охватывающего if-блока или <see langword="null"/>, если вызов не находится внутри условного блока.</summary>
    public required string? CallCondition { get; init; }

    /// <summary>Ветка охватывающего if-блока: <c>then</c> (условие выполнилось), <c>else</c> (условие не выполнилось), или <see langword="null"/>, если условия нет.</summary>
    public required string? Branch { get; init; }
}
