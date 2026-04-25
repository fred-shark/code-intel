using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeIntel.Analysis;
using CodeIntel.Contracts;

namespace CodeIntel.Analysis.Tests;

/// <summary>
/// Проверяет поведение сервиса поиска ссылок через Roslyn.
/// </summary>
public sealed class FindReferencesServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "codeintel-find-references-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FindAsync_ReturnsDeclarationAndReferences_ForUniqueClass()
    {
        var solutionPath = CreateSolution(
            appFiles: new Dictionary<string, string>
            {
                ["Widget.cs"] = """
                    namespace Sample.Core;

                    public class Widget
                    {
                    }
                    """,
                ["WidgetConsumer.cs"] = """
                    namespace Sample.Core;

                    public sealed class WidgetConsumer
                    {
                        private readonly Widget _widget;

                        public WidgetConsumer(Widget widget)
                        {
                            _widget = widget;
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new FindReferencesService();

        var response = await service.FindAsync(solutionPath, "Widget");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Equal("Widget", response.Symbol);
        Assert.NotNull(response.Declaration);
        Assert.Equal("Widget", response.Declaration.Symbol);
        Assert.Equal(SymbolKindDto.Class, response.Declaration.Kind);
        Assert.Equal("App", response.Declaration.Project);
        Assert.EndsWith("Widget.cs", response.Declaration.FilePath, StringComparison.Ordinal);
        Assert.NotNull(response.Declaration.FullyQualifiedName);
        Assert.True(response.ReferenceCount >= 2);
        Assert.Equal(response.ReferenceCount, response.References.Count);
        Assert.All(response.References, reference => Assert.Equal("Widget", reference.Symbol));
        Assert.All(response.References, reference => Assert.Equal(response.Declaration.FullyQualifiedName, reference.ReferencedSymbolFullyQualifiedName));
        Assert.Contains(response.References, reference => reference.FilePath.EndsWith("WidgetConsumer.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindAsync_ReturnsEmptyResponse_WhenSymbolIsNotFound()
    {
        var solutionPath = CreateSolution(
            appFiles: new Dictionary<string, string>
            {
                ["KnownType.cs"] = """
                    namespace Sample.Core;

                    public interface KnownType
                    {
                    }
                    """
            },
            projectName: "App");
        var service = new FindReferencesService();

        var response = await service.FindAsync(solutionPath, "MissingType");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Equal("MissingType", response.Symbol);
        Assert.Null(response.Declaration);
        Assert.Empty(response.References);
        Assert.Equal(0, response.ReferenceCount);
    }

    [Fact]
    public async Task FindAsync_Throws_WhenSymbolIsAmbiguous()
    {
        var solutionPath = CreateAmbiguousSolution();
        var service = new FindReferencesService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.FindAsync(solutionPath, "SharedType"));

        Assert.Contains("Multiple matching type declarations were found", exception.Message);
        Assert.Contains("SharedType", exception.Message);
        Assert.Contains("First", exception.Message);
        Assert.Contains("Second", exception.Message);
    }

    /// <summary>
    /// Проверяет, что fully qualified запрос снимает неоднозначность при поиске ссылок.
    /// </summary>
    [Fact]
    public async Task FindAsync_ReturnsDeclarationAndReferences_ForFullyQualifiedSymbol()
    {
        var solutionPath = CreateQualifiedResolutionSolution();
        var service = new FindReferencesService();

        var response = await service.FindAsync(solutionPath, "Sample.Second.SharedType");

        Assert.NotNull(response.Declaration);
        Assert.Equal("global::Sample.Second.SharedType", response.Declaration.FullyQualifiedName);
        Assert.True(response.ReferenceCount >= 2);
        Assert.All(response.References, reference => Assert.Equal("global::Sample.Second.SharedType", reference.ReferencedSymbolFullyQualifiedName));
        Assert.Contains(response.References, reference => reference.FilePath.EndsWith("SecondConsumer.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// Проверяет поддержку fully qualified запроса с префиксом global::.
    /// </summary>
    [Fact]
    public async Task FindAsync_ReturnsDeclarationAndReferences_ForGlobalQualifiedSymbol()
    {
        var solutionPath = CreateQualifiedResolutionSolution();
        var service = new FindReferencesService();

        var response = await service.FindAsync(solutionPath, "global::Sample.Second.SharedType");

        Assert.NotNull(response.Declaration);
        Assert.Equal("global::Sample.Second.SharedType", response.Declaration.FullyQualifiedName);
        Assert.Contains(response.References, reference => reference.FilePath.EndsWith("SecondConsumer.cs", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string CreateAmbiguousSolution()
    {
        Directory.CreateDirectory(_tempRoot);

        var firstProjectDir = Path.Combine(_tempRoot, "src", "First");
        Directory.CreateDirectory(firstProjectDir);
        File.WriteAllText(
            Path.Combine(firstProjectDir, "SharedType.cs"),
            """
            namespace Sample.First;

            public class SharedType
            {
            }
            """);
        File.WriteAllText(
            Path.Combine(firstProjectDir, "First.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var secondProjectDir = Path.Combine(_tempRoot, "src", "Second");
        Directory.CreateDirectory(secondProjectDir);
        File.WriteAllText(
            Path.Combine(secondProjectDir, "SharedType.cs"),
            """
            namespace Sample.Second;

            public interface SharedType
            {
            }
            """);
        File.WriteAllText(
            Path.Combine(secondProjectDir, "Second.csproj"),
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
            """
            <Solution>
              <Project Path="src/First/First.csproj" />
              <Project Path="src/Second/Second.csproj" />
            </Solution>
            """);

        return solutionPath;
    }

    private string CreateQualifiedResolutionSolution()
    {
        Directory.CreateDirectory(_tempRoot);

        var firstProjectDir = Path.Combine(_tempRoot, "src", "First");
        Directory.CreateDirectory(firstProjectDir);
        File.WriteAllText(
            Path.Combine(firstProjectDir, "SharedType.cs"),
            """
            namespace Sample.First;

            public class SharedType
            {
            }
            """);
        File.WriteAllText(
            Path.Combine(firstProjectDir, "FirstConsumer.cs"),
            """
            namespace Sample.First;

            public sealed class FirstConsumer
            {
                private readonly SharedType _value = new();
            }
            """);
        File.WriteAllText(
            Path.Combine(firstProjectDir, "First.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var secondProjectDir = Path.Combine(_tempRoot, "src", "Second");
        Directory.CreateDirectory(secondProjectDir);
        File.WriteAllText(
            Path.Combine(secondProjectDir, "SharedType.cs"),
            """
            namespace Sample.Second;

            public class SharedType
            {
            }
            """);
        File.WriteAllText(
            Path.Combine(secondProjectDir, "SecondConsumer.cs"),
            """
            namespace Sample.Second;

            public sealed class SecondConsumer
            {
                private readonly SharedType _value = new();
            }
            """);
        File.WriteAllText(
            Path.Combine(secondProjectDir, "Second.csproj"),
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
            """
            <Solution>
              <Project Path="src/First/First.csproj" />
              <Project Path="src/Second/Second.csproj" />
            </Solution>
            """);

        return solutionPath;
    }

    private string CreateSolution(IReadOnlyDictionary<string, string> appFiles, string projectName)
    {
        Directory.CreateDirectory(_tempRoot);
        var projectDir = Path.Combine(_tempRoot, "src", projectName);
        Directory.CreateDirectory(projectDir);

        foreach (var (fileName, content) in appFiles)
        {
            File.WriteAllText(Path.Combine(projectDir, fileName), content);
        }

        File.WriteAllText(
            Path.Combine(projectDir, $"{projectName}.csproj"),
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
