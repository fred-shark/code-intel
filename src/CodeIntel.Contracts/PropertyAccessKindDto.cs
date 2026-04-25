namespace CodeIntel.Contracts;

/// <summary>
/// Определяет режим доступа к свойству для трассировки чтений и записей.
/// </summary>
public enum PropertyAccessKindDto
{
    /// <summary>Чтение значения свойства.</summary>
    Get,

    /// <summary>Запись значения свойства.</summary>
    Set,

    /// <summary>Одновременная трассировка чтений и записей.</summary>
    Both
}
