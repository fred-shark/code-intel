using System.Xml.Linq;

namespace CodeIntel.Loader;

/// <summary>
/// Определяет, следует ли считать проект тестовым по имени и зависимостям.
/// </summary>
public static class TestProjectClassifier
{
    private static readonly HashSet<string> TestPackageAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.NET.Test.Sdk",
        "xunit",
        "xunit.runner.visualstudio",
        "NUnit",
        "NUnit3TestAdapter",
        "nunit.framework",
        "MSTest.TestFramework",
        "MSTest.TestAdapter",
        "Microsoft.VisualStudio.QualityTools.UnitTestFramework"
    };

    /// <summary>
    /// Определяет, следует ли считать имя проекта тестовым по MVP-эвристике имени.
    /// </summary>
    /// <param name="projectName">Имя проекта.</param>
    /// <returns><see langword="true"/>, если имя проекта выглядит как тестовое; иначе <see langword="false"/>.</returns>
    public static bool IsTestProjectName(string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        return HasTestProjectName(projectName);
    }

    /// <summary>
    /// Определяет, является ли проект тестовым, используя имя проекта и путь к файлу проекта.
    /// </summary>
    /// <param name="projectName">Имя проекта.</param>
    /// <param name="projectFilePath">Абсолютный путь к файлу проекта.</param>
    /// <returns><see langword="true"/>, если проект классифицирован как тестовый; иначе <see langword="false"/>.</returns>
    public static bool IsTestProject(string projectName, string projectFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        var document = XDocument.Load(projectFilePath);
        return IsTestProject(projectName, document);
    }

    /// <summary>
    /// Определяет, является ли проект тестовым, используя имя проекта и содержимое файла проекта.
    /// </summary>
    /// <param name="projectName">Имя проекта.</param>
    /// <param name="projectDocument">XML-документ файла проекта.</param>
    /// <returns><see langword="true"/>, если проект классифицирован как тестовый; иначе <see langword="false"/>.</returns>
    public static bool IsTestProject(string projectName, XDocument projectDocument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentNullException.ThrowIfNull(projectDocument);

        return HasTestProjectName(projectName) || HasKnownTestPackageReference(projectDocument);
    }

    private static bool HasTestProjectName(string projectName)
    {
        return projectName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
               projectName.EndsWith(".UnitTests", StringComparison.OrdinalIgnoreCase) ||
               projectName.Contains(".Tests.", StringComparison.OrdinalIgnoreCase) ||
               projectName.Contains(".UnitTests.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasKnownTestPackageReference(XDocument projectDocument)
    {
        return projectDocument
            .Descendants()
            .Where(static element => element.Name.LocalName == "PackageReference")
            .Select(static element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value)
            .Where(static packageName => !string.IsNullOrWhiteSpace(packageName))
            .Any(packageName => TestPackageAllowlist.Contains(packageName!));
    }
}
