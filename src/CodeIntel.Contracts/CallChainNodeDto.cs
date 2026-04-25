using System.Collections.Generic;

namespace CodeIntel.Contracts;

/// <summary>
/// Представляет узел дерева вызовов — вызывающий метод с информацией о точке вызова и его собственными вызывающими.
/// </summary>
public sealed record CallChainNodeDto
{
    /// <summary>Имя типа, содержащего вызывающий метод.</summary>
    public required string ContainingType { get; init; }

    /// <summary>Полностью квалифицированное имя содержащего типа.</summary>
    public required string? FullyQualifiedContainingType { get; init; }

    /// <summary>Имя вызывающего метода.</summary>
    public required string Method { get; init; }

    /// <summary>Название проекта, в котором объявлен вызывающий метод.</summary>
    public required string Project { get; init; }

    /// <summary>Абсолютный путь к файлу с объявлением вызывающего метода.</summary>
    public required string FilePath { get; init; }

    /// <summary>Номер строки объявления вызывающего метода (1-индексированный).</summary>
    public required int Line { get; init; }

    /// <summary>Номер столбца объявления вызывающего метода (1-индексированный).</summary>
    public required int Column { get; init; }

    /// <summary>Описание точки вызова целевого метода внутри данного метода.</summary>
    public required CallSiteDto CallSite { get; init; }

    /// <summary>Является ли данный метод точкой входа (не имеет вызывающих внутри решения).</summary>
    public required bool IsEntryPoint { get; init; }

    /// <summary>Вызывающие данного метода. Пустой список для точек входа.</summary>
    public required IReadOnlyList<CallChainNodeDto> CalledBy { get; init; }
}
