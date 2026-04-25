namespace CodeIntel.Contracts;

/// <summary>
/// Определяет поддерживаемый фреймворк регистрации зависимостей.
/// </summary>
public enum RegistrationFrameworkDto
{
    /// <summary>Регистрация через ASP.NET Core dependency injection.</summary>
    AspNetCoreDI,

    /// <summary>Регистрация через Castle Windsor.</summary>
    CastleWindsor
}
