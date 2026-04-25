using CodeIntel.Loader;

namespace CodeIntel.Loader.Tests;

/// <summary>
/// Проверяет загрузку сводки по решению из файлов `.sln` и `.slnx`.
/// </summary>
public sealed class SolutionSummaryLoaderTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "codeintel-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_ParsesSlnxProjectsAndTargetFrameworks()
    {
        Directory.CreateDirectory(_tempRoot);
        CreateProject("src/App/App.csproj", "net10.0");
        CreateProject("src/Lib/Lib.csproj", "net8.0;net10.0");
        File.WriteAllText(
            Path.Combine(_tempRoot, "sample.slnx"),
            """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/App/App.csproj" />
                <Project Path="src/Lib/Lib.csproj" />
              </Folder>
            </Solution>
            """);

        var loader = new SolutionSummaryLoader();

        var summary = await loader.LoadAsync(Path.Combine(_tempRoot, "sample.slnx"));

        Assert.Equal(Path.Combine(_tempRoot, "sample.slnx"), summary.SolutionPath);
        Assert.Collection(
            summary.Projects,
            project =>
            {
                Assert.Equal("App", project.Name);
                Assert.Equal(Path.Combine(_tempRoot, "src/App/App.csproj"), project.Path);
                Assert.Equal(["net10.0"], project.TargetFrameworks);
                Assert.False(project.IsTestProject);
            },
            project =>
            {
                Assert.Equal("Lib", project.Name);
                Assert.Equal(Path.Combine(_tempRoot, "src/Lib/Lib.csproj"), project.Path);
                Assert.Equal(["net8.0", "net10.0"], project.TargetFrameworks);
                Assert.False(project.IsTestProject);
            });
    }

    [Fact]
    public async Task LoadAsync_ParsesSlnProjectsAndUsesSolutionProjectName()
    {
        Directory.CreateDirectory(_tempRoot);
        CreateProject("src/Custom.Project/Custom.Project.csproj", "net10.0", assemblyName: "Assembly.Different");
        File.WriteAllText(
            Path.Combine(_tempRoot, "sample.sln"),
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Solution.Name", "src\Custom.Project\Custom.Project.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            """);

        var loader = new SolutionSummaryLoader();

        var summary = await loader.LoadAsync(Path.Combine(_tempRoot, "sample.sln"));

        var project = Assert.Single(summary.Projects);
        Assert.Equal("Solution.Name", project.Name);
        Assert.Equal(Path.Combine(_tempRoot, "src/Custom.Project/Custom.Project.csproj"), project.Path);
        Assert.Equal(["net10.0"], project.TargetFrameworks);
        Assert.False(project.IsTestProject);
    }

    /// <summary>
    /// Проверяет, что проект с суффиксом `.Tests` помечается как тестовый.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ClassifiesTestProject_ByProjectName()
    {
        Directory.CreateDirectory(_tempRoot);
        CreateProject("tests/App.Tests/App.Tests.csproj", "net10.0");
        File.WriteAllText(
            Path.Combine(_tempRoot, "sample.slnx"),
            """
            <Solution>
              <Project Path="tests/App.Tests/App.Tests.csproj" />
            </Solution>
            """);

        var loader = new SolutionSummaryLoader();

        var summary = await loader.LoadAsync(Path.Combine(_tempRoot, "sample.slnx"));

        var project = Assert.Single(summary.Projects);
        Assert.Equal("App.Tests", project.Name);
        Assert.True(project.IsTestProject);
    }

    /// <summary>
    /// Проверяет, что проект с тестовым пакетом помечается как тестовый.
    /// </summary>
    [Fact]
    public async Task LoadAsync_ClassifiesTestProject_ByPackageReference()
    {
        Directory.CreateDirectory(_tempRoot);
        CreateProject(
            "src/IntegrationHarness/IntegrationHarness.csproj",
            "net10.0",
            packageReferences: ["Microsoft.NET.Test.Sdk"]);
        File.WriteAllText(
            Path.Combine(_tempRoot, "sample.slnx"),
            """
            <Solution>
              <Project Path="src/IntegrationHarness/IntegrationHarness.csproj" />
            </Solution>
            """);

        var loader = new SolutionSummaryLoader();

        var summary = await loader.LoadAsync(Path.Combine(_tempRoot, "sample.slnx"));

        var project = Assert.Single(summary.Projects);
        Assert.Equal("IntegrationHarness", project.Name);
        Assert.True(project.IsTestProject);
    }

    /// <summary>
    /// Освобождает временные файлы, созданные во время тестов.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private void CreateProject(
        string relativePath,
        string targetFrameworks,
        string? assemblyName = null,
        IReadOnlyList<string>? packageReferences = null)
    {
        var fullPath = Path.Combine(_tempRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var assemblyNameElement = string.IsNullOrWhiteSpace(assemblyName)
            ? string.Empty
            : $"    <AssemblyName>{assemblyName}</AssemblyName>{Environment.NewLine}";
        var packageReferenceElements = packageReferences is null || packageReferences.Count == 0
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                packageReferences.Select(packageReference => $"    <PackageReference Include=\"{packageReference}\" Version=\"1.0.0\" />"));
        var itemGroup = string.IsNullOrWhiteSpace(packageReferenceElements)
            ? string.Empty
            : $"{Environment.NewLine}  <ItemGroup>{Environment.NewLine}{packageReferenceElements}{Environment.NewLine}  </ItemGroup>";

        File.WriteAllText(
            fullPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
            {{assemblyNameElement}}    <TargetFrameworks>{{targetFrameworks}}</TargetFrameworks>
              </PropertyGroup>
            {{itemGroup}}
            </Project>
            """);
    }
}
