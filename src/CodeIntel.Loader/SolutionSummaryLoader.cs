using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeIntel.Contracts;

namespace CodeIntel.Loader;

/// <summary>
/// Загружает решение и извлекает список проектов с их целевыми платформами.
/// </summary>
public sealed partial class SolutionSummaryLoader : ISolutionSummaryLoader
{
    /// <summary>
    /// Загружает файл решения и строит DTO со сводной информацией по проектам.
    /// </summary>
    /// <param name="solutionPath">Путь к файлу решения `.sln` или `.slnx`.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <returns>Сводка по решению.</returns>
    public Task<SolutionSummaryDto> LoadAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);

        var fullSolutionPath = Path.GetFullPath(solutionPath);
        if (!File.Exists(fullSolutionPath))
        {
            throw new FileNotFoundException($"Solution file was not found: {fullSolutionPath}", fullSolutionPath);
        }

        var extension = Path.GetExtension(fullSolutionPath);
        var projectEntries = extension.ToLowerInvariant() switch
        {
            ".sln" => LoadSlnProjectEntries(fullSolutionPath),
            ".slnx" => LoadSlnxProjectEntries(fullSolutionPath),
            _ => throw new NotSupportedException($"Unsupported solution format '{extension}'. Expected .sln or .slnx.")
        };

        var projects = projectEntries
            .Select(entry => CreateProjectSummary(entry, fullSolutionPath))
            .ToArray();

        return Task.FromResult(new SolutionSummaryDto
        {
            SolutionPath = fullSolutionPath,
            Projects = projects
        });
    }

    private static ProjectSummaryDto CreateProjectSummary(ProjectEntry entry, string solutionPath)
    {
        var projectPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solutionPath)!, entry.RelativePath));
        var document = XDocument.Load(projectPath);

        var name = ResolveProjectName(entry, projectPath, document);
        var targetFrameworks = ResolveTargetFrameworks(document);
        var isTestProject = TestProjectClassifier.IsTestProject(name, document);

        return new ProjectSummaryDto
        {
            Name = name,
            Path = projectPath,
            TargetFrameworks = targetFrameworks,
            IsTestProject = isTestProject
        };
    }

    private static string ResolveProjectName(ProjectEntry entry, string projectPath, XDocument document)
    {
        if (!string.IsNullOrWhiteSpace(entry.Name))
        {
            return entry.Name;
        }

        var assemblyName = document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "AssemblyName")
            ?.Value
            .Trim();

        return string.IsNullOrWhiteSpace(assemblyName)
            ? Path.GetFileNameWithoutExtension(projectPath)
            : assemblyName;
    }

    private static IReadOnlyList<string> ResolveTargetFrameworks(XDocument document)
    {
        var frameworks = document
            .Descendants()
            .Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return frameworks;
    }

    private static IReadOnlyList<ProjectEntry> LoadSlnxProjectEntries(string solutionPath)
    {
        var document = XDocument.Load(solutionPath);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new ProjectEntry(null, path!))
            .ToArray();
    }

    private static IReadOnlyList<ProjectEntry> LoadSlnProjectEntries(string solutionPath)
    {
        return File.ReadLines(solutionPath)
            .Select(line => SlnProjectRegex().Match(line))
            .Where(match => match.Success)
            .Select(match => new ProjectEntry(
                match.Groups["name"].Value,
                match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar)))
            .Where(entry => entry.RelativePath.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    [GeneratedRegex("^Project\\(\"\\{[^\\}]+\\}\"\\) = \"(?<name>[^\"]+)\", \"(?<path>[^\"]+)\", \"\\{[^\\}]+\\}\"$")]
    private static partial Regex SlnProjectRegex();

    private sealed record ProjectEntry(string? Name, string RelativePath);
}
