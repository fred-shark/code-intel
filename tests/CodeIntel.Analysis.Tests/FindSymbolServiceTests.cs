using CodeIntel.Analysis;
using CodeIntel.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeIntel.Analysis.Tests;

/// <summary>
/// Проверяет поведение сервиса поиска символов через Roslyn.
/// </summary>
public sealed class FindSymbolServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "codeintel-analysis-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FindAsync_ReturnsClassInterfaceEnumDetails()
    {
        var solutionPath = CreateSolutionWithSymbols();
        var service = new FindSymbolService();

        var response = await service.FindAsync(solutionPath, "Target");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Equal("Target", response.Name);
        Assert.Equal(3, response.Results.Count);

        var classResult = Assert.Single(response.Results.Where(r => r.Kind == SymbolKindDto.Class));
        Assert.Equal("App", classResult.Project);
        Assert.Contains("global::", classResult.FullyQualifiedName ?? string.Empty);
        Assert.True(classResult.Line > 0);
        Assert.True(classResult.Column > 0);

        var interfaceResult = Assert.Single(response.Results.Where(r => r.Kind == SymbolKindDto.Interface));
        Assert.EndsWith("InterfaceSymbols.Target", interfaceResult.FullyQualifiedName);

        var enumResult = Assert.Single(response.Results.Where(r => r.Kind == SymbolKindDto.Enum));
        Assert.EndsWith("EnumSymbols.Target", enumResult.FullyQualifiedName);
    }

    [Fact]
    public async Task FindAsync_RespectsMaxResults()
    {
        var solutionPath = CreateSolutionWithManySymbols(25);
        var service = new FindSymbolService();

        var response = await service.FindAsync(solutionPath, "Target");

        Assert.Equal(20, response.Results.Count);
        Assert.All(response.Results, result => Assert.Equal("Target", result.Name));
    }

    [Fact]
    public async Task FindAsync_ReturnsEmptyResults_ForNamespaceOrProjectNameQuery()
    {
        var solutionPath = CreateLoaderSolution();
        var service = new FindSymbolService();

        var response = await service.FindAsync(solutionPath, "CodeIntel.Loader");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Equal("CodeIntel.Loader", response.Name);
        Assert.Empty(response.Results);
    }

    [Fact]
    public async Task FindAsync_DeduplicatesSymbolsImportedThroughProjectReferences()
    {
        var solutionPath = CreateSolutionWithProjectReference();
        var service = new FindSymbolService();

        var response = await service.FindAsync(solutionPath, "SharedContract");

        var result = Assert.Single(response.Results);
        Assert.Equal("SharedContract", result.Name);
        Assert.Equal(SymbolKindDto.Interface, result.Kind);
        Assert.Equal("Core", result.Project);
        Assert.EndsWith("SharedContract.cs", result.FilePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Проверяет поиск по полностью квалифицированному имени типа.
    /// </summary>
    [Fact]
    public async Task FindAsync_ReturnsSingleResult_ForFullyQualifiedQuery()
    {
        var solutionPath = CreateSolutionWithDuplicateShortNames();
        var service = new FindSymbolService();

        var response = await service.FindAsync(solutionPath, "Sample.Second.Target");

        var result = Assert.Single(response.Results);
        Assert.Equal("Target", result.Name);
        Assert.Equal("global::Sample.Second.Target", result.FullyQualifiedName);
    }

    /// <summary>
    /// Проверяет поиск по fully qualified имени с префиксом global::.
    /// </summary>
    [Fact]
    public async Task FindAsync_ReturnsSingleResult_ForGlobalQualifiedQuery()
    {
        var solutionPath = CreateSolutionWithDuplicateShortNames();
        var service = new FindSymbolService();

        var response = await service.FindAsync(solutionPath, "global::Sample.Second.Target");

        var result = Assert.Single(response.Results);
        Assert.Equal("global::Sample.Second.Target", result.FullyQualifiedName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string CreateSolutionWithSymbols()
    {
        var files = new Dictionary<string, string>
        {
            ["ClassSymbol.cs"] = """
                namespace Sample.ClassSymbols
                {
                    public class Target
                    {
                    }
                }
                """,
            ["InterfaceSymbol.cs"] = """
                namespace Sample.InterfaceSymbols
                {
                    public interface Target
                    {
                    }
                }
                """,
            ["EnumSymbol.cs"] = """
                namespace Sample.EnumSymbols
                {
                    public enum Target
                    {
                        None,
                        Value
                    }
                }
                """
        };

        return CreateSolution(files);
    }

    private string CreateSolutionWithManySymbols(int symbolCount)
    {
        var files = new Dictionary<string, string>();
        for (var i = 0; i < symbolCount; i++)
        {
            var fileName = $"GeneratedSymbol{i}.cs";
            files[fileName] = $$"""
                namespace Sample.GeneratedSymbols.Namespace{{i}}
                {
                    public class Target
                    {
                    }
                }
                """;
        }

        return CreateSolution(files);
    }

    private string CreateLoaderSolution()
    {
        var files = new Dictionary<string, string>
        {
            ["SolutionSummaryLoader.cs"] = """
                namespace CodeIntel.Loader;

                public sealed class SolutionSummaryLoader
                {
                }
                """,
            ["ISolutionSummaryLoader.cs"] = """
                namespace CodeIntel.Loader;

                public interface ISolutionSummaryLoader
                {
                }
                """
        };

        return CreateSolution(files, "CodeIntel.Loader");
    }

    private string CreateSolutionWithDuplicateShortNames()
    {
        var files = new Dictionary<string, string>
        {
            ["FirstTarget.cs"] = """
                namespace Sample.First;

                public class Target
                {
                }
                """,
            ["SecondTarget.cs"] = """
                namespace Sample.Second;

                public class Target
                {
                }
                """
        };

        return CreateSolution(files);
    }

    private string CreateSolutionWithProjectReference()
    {
        Directory.CreateDirectory(_tempRoot);

        var coreProjectDir = Path.Combine(_tempRoot, "src", "Core");
        Directory.CreateDirectory(coreProjectDir);
        File.WriteAllText(
            Path.Combine(coreProjectDir, "SharedContract.cs"),
            """
            namespace Sample.Contracts;

            public interface SharedContract
            {
            }
            """);
        File.WriteAllText(
            Path.Combine(coreProjectDir, "Core.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var appProjectDir = Path.Combine(_tempRoot, "src", "App");
        Directory.CreateDirectory(appProjectDir);
        File.WriteAllText(
            Path.Combine(appProjectDir, "Consumer.cs"),
            """
            namespace Sample.App;

            public sealed class Consumer : Sample.Contracts.SharedContract
            {
            }
            """);
        File.WriteAllText(
            Path.Combine(appProjectDir, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Core\Core.csproj" />
              </ItemGroup>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var solutionPath = Path.Combine(_tempRoot, "sample.slnx");
        File.WriteAllText(
            solutionPath,
            """
            <Solution>
              <Project Path="src/Core/Core.csproj" />
              <Project Path="src/App/App.csproj" />
            </Solution>
            """);

        return solutionPath;
    }

    private string CreateSolution(IReadOnlyDictionary<string, string> sources, string projectName = "App")
    {
        Directory.CreateDirectory(_tempRoot);
        var projectDir = Path.Combine(_tempRoot, "src", projectName);
        Directory.CreateDirectory(projectDir);

        foreach (var (fileName, content) in sources)
        {
            var filePath = Path.Combine(projectDir, fileName);
            File.WriteAllText(filePath, content);
        }

        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var solutionPath = Path.Combine(_tempRoot, "sample.slnx");
        File.WriteAllText(
            solutionPath,
            $"""
            <Solution>
              <Project Path="src/{projectName}/{projectName}.csproj" />
            </Solution>
            """);

        return solutionPath;
    }
}
