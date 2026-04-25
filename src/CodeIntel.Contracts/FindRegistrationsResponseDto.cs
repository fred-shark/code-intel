using System.Collections.Generic;

namespace CodeIntel.Contracts;

/// <summary>
/// Возвращает структуру ответа для команды find-registrations.
/// </summary>
public sealed record FindRegistrationsResponseDto
{
    /// <summary>Путь к загруженному решению.</summary>
    public required string SolutionPath { get; init; }

    /// <summary>Запрошенное имя символа.</summary>
    public required string Symbol { get; init; }

    /// <summary>Объявление найденного символа или <see langword="null"/>, если символ не найден.</summary>
    public required FindRegistrationsDeclarationDto? Declaration { get; init; }

    /// <summary>Список найденных регистраций зависимости.</summary>
    public required IReadOnlyList<FindRegistrationsResultDto> Registrations { get; init; }

    /// <summary>Количество найденных регистраций.</summary>
    public required int RegistrationCount { get; init; }
}
