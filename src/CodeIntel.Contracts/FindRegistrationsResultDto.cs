namespace CodeIntel.Contracts;

/// <summary>
/// Представляет одну найденную регистрацию зависимости в коде решения.
/// </summary>
public sealed record FindRegistrationsResultDto
{
    /// <summary>Имя или отображаемая сигнатура зарегистрированного сервиса.</summary>
    public required string ServiceSymbol { get; init; }

    /// <summary>Имя или отображаемая сигнатура зарегистрированной реализации.</summary>
    public required string ImplementationSymbol { get; init; }

    /// <summary>Определенный lifetime регистрации, если он был распознан.</summary>
    public required string? Lifetime { get; init; }

    /// <summary>Фреймворк, в котором была найдена регистрация.</summary>
    public required RegistrationFrameworkDto RegistrationFramework { get; init; }

    /// <summary>Название проекта, содержащего регистрацию.</summary>
    public required string Project { get; init; }

    /// <summary>Абсолютный путь к файлу, содержащему регистрацию.</summary>
    public required string FilePath { get; init; }

    /// <summary>Номер строки регистрации (1-индексированный).</summary>
    public required int Line { get; init; }

    /// <summary>Номер столбца регистрации (1-индексированный).</summary>
    public required int Column { get; init; }
}
