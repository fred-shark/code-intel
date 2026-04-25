using System;
using System.Collections.Generic;
using System.IO;
using CodeIntel.Analysis;
using CodeIntel.Contracts;

namespace CodeIntel.Analysis.Tests;

/// <summary>
/// Проверяет поведение сервиса поиска реализаций через Roslyn.
/// </summary>
public sealed class FindImplementationsServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "codeintel-find-implementations-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FindAsync_ReturnsImplementations_ForInterface()
    {
        var solutionPath = CreateInterfaceSolution();
        var service = new FindImplementationsService();

        var response = await service.FindAsync(solutionPath, "IHandler");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Equal("IHandler", response.Symbol);
        Assert.NotNull(response.Declaration);
        Assert.Equal("IHandler", response.Declaration.Symbol);
        Assert.Equal(SymbolKindDto.Interface, response.Declaration.Kind);
        Assert.Equal(2, response.ImplementationCount);
        Assert.Equal(response.ImplementationCount, response.Implementations.Count);
        Assert.Contains(response.Implementations, implementation => implementation.Symbol == "ConsoleHandler");
        Assert.Contains(response.Implementations, implementation => implementation.Symbol == "FileHandler");
    }

    /// <summary>
    /// Проверяет, что реализации из тестовых проектов скрываются по умолчанию.
    /// </summary>
    [Fact]
    public async Task FindAsync_ExcludesImplementationsFromTestProjects_ByDefault()
    {
        var solutionPath = CreateInterfaceSolutionWithTestProject();
        var service = new FindImplementationsService();

        var response = await service.FindAsync(solutionPath, "IHandler");

        Assert.Equal(1, response.ImplementationCount);
        var implementation = Assert.Single(response.Implementations);
        Assert.Equal("ConsoleHandler", implementation.Symbol);
        Assert.DoesNotContain(response.Implementations, item => item.Project == "App.Tests");
    }

    /// <summary>
    /// Проверяет, что реализации из тестовых проектов возвращаются при включении флага.
    /// </summary>
    [Fact]
    public async Task FindAsync_IncludesImplementationsFromTestProjects_WhenRequested()
    {
        var solutionPath = CreateInterfaceSolutionWithTestProject();
        var service = new FindImplementationsService();

        var response = await service.FindAsync(solutionPath, "IHandler", includeTests: true);

        Assert.Equal(2, response.ImplementationCount);
        Assert.Contains(response.Implementations, implementation => implementation.Symbol == "ConsoleHandler");
        Assert.Contains(response.Implementations, implementation => implementation.Symbol == "TestHandler");
    }

    [Fact]
    public async Task FindAsync_ReturnsImplementations_ForAbstractClass()
    {
        var solutionPath = CreateAbstractClassSolution();
        var service = new FindImplementationsService();

        var response = await service.FindAsync(solutionPath, "CommandBase");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Equal("CommandBase", response.Symbol);
        Assert.NotNull(response.Declaration);
        Assert.Equal(SymbolKindDto.Class, response.Declaration.Kind);
        Assert.Equal(1, response.ImplementationCount);
        var implementation = Assert.Single(response.Implementations);
        Assert.Equal("PrintCommand", implementation.Symbol);
        Assert.Equal(SymbolKindDto.Class, implementation.Kind);
    }

    [Fact]
    public async Task FindAsync_ReturnsDeclarationAndEmptyImplementations_ForConcreteClass()
    {
        var solutionPath = CreateConcreteClassSolution();
        var service = new FindImplementationsService();

        var response = await service.FindAsync(solutionPath, "Worker");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Equal("Worker", response.Symbol);
        Assert.NotNull(response.Declaration);
        Assert.Equal(SymbolKindDto.Class, response.Declaration.Kind);
        Assert.Empty(response.Implementations);
        Assert.Equal(0, response.ImplementationCount);
    }

    [Fact]
    public async Task FindAsync_ReturnsEmptyResponse_WhenSymbolIsNotFound()
    {
        var solutionPath = CreateInterfaceSolution();
        var service = new FindImplementationsService();

        var response = await service.FindAsync(solutionPath, "MissingType");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Equal("MissingType", response.Symbol);
        Assert.Null(response.Declaration);
        Assert.Empty(response.Implementations);
        Assert.Equal(0, response.ImplementationCount);
    }

    [Fact]
    public async Task FindAsync_Throws_WhenSymbolIsAmbiguous()
    {
        var solutionPath = CreateAmbiguousSolution();
        var service = new FindImplementationsService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.FindAsync(solutionPath, "SharedType"));

        Assert.Contains("Multiple matching type declarations were found", exception.Message);
        Assert.Contains("SharedType", exception.Message);
        Assert.Contains("First", exception.Message);
        Assert.Contains("Second", exception.Message);
    }

    /// <summary>
    /// Проверяет, что fully qualified запрос выбирает нужный интерфейс среди одноименных типов.
    /// </summary>
    [Fact]
    public async Task FindAsync_ReturnsImplementations_ForFullyQualifiedSymbol()
    {
        var solutionPath = CreateQualifiedResolutionSolution();
        var service = new FindImplementationsService();

        var response = await service.FindAsync(solutionPath, "Sample.Second.IHandler");

        Assert.NotNull(response.Declaration);
        Assert.Equal("global::Sample.Second.IHandler", response.Declaration.FullyQualifiedName);
        var implementation = Assert.Single(response.Implementations);
        Assert.Equal("SecondHandler", implementation.Symbol);
    }

    /// <summary>
    /// Проверяет поддержку fully qualified имени с префиксом global::.
    /// </summary>
    [Fact]
    public async Task FindAsync_ReturnsImplementations_ForGlobalQualifiedSymbol()
    {
        var solutionPath = CreateQualifiedResolutionSolution();
        var service = new FindImplementationsService();

        var response = await service.FindAsync(solutionPath, "global::Sample.Second.IHandler");

        Assert.NotNull(response.Declaration);
        Assert.Equal("global::Sample.Second.IHandler", response.Declaration.FullyQualifiedName);
        Assert.Contains(response.Implementations, implementation => implementation.Symbol == "SecondHandler");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string CreateInterfaceSolution()
    {
        Directory.CreateDirectory(_tempRoot);

        var abstractionsProjectDir = Path.Combine(_tempRoot, "src", "Abstractions");
        Directory.CreateDirectory(abstractionsProjectDir);
        File.WriteAllText(
            Path.Combine(abstractionsProjectDir, "IHandler.cs"),
            """
            namespace Sample.Abstractions;

            public interface IHandler
            {
                void Handle();
            }
            """);
        File.WriteAllText(
            Path.Combine(abstractionsProjectDir, "Abstractions.csproj"),
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
            Path.Combine(appProjectDir, "ConsoleHandler.cs"),
            """
            using Sample.Abstractions;

            namespace Sample.App;

            public sealed class ConsoleHandler : IHandler
            {
                public void Handle()
                {
                }
            }
            """);
        File.WriteAllText(
            Path.Combine(appProjectDir, "FileHandler.cs"),
            """
            using Sample.Abstractions;

            namespace Sample.App;

            public sealed class FileHandler : IHandler
            {
                public void Handle()
                {
                }
            }
            """);
        File.WriteAllText(
            Path.Combine(appProjectDir, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Abstractions\Abstractions.csproj" />
              </ItemGroup>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        return WriteSolution(
            """
            <Solution>
              <Project Path="src/Abstractions/Abstractions.csproj" />
              <Project Path="src/App/App.csproj" />
            </Solution>
            """);
    }

    private string CreateAbstractClassSolution()
    {
        return CreateSingleProjectSolution(
            "App",
            new Dictionary<string, string>
            {
                ["CommandBase.cs"] = """
                    namespace Sample.App;

                    public abstract class CommandBase
                    {
                        public abstract void Execute();
                    }
                    """,
                ["PrintCommand.cs"] = """
                    namespace Sample.App;

                    public sealed class PrintCommand : CommandBase
                    {
                        public override void Execute()
                        {
                        }
                    }
                    """
            });
    }

    private string CreateInterfaceSolutionWithTestProject()
    {
        Directory.CreateDirectory(_tempRoot);

        var abstractionsProjectDir = Path.Combine(_tempRoot, "src", "Abstractions");
        Directory.CreateDirectory(abstractionsProjectDir);
        File.WriteAllText(
            Path.Combine(abstractionsProjectDir, "IHandler.cs"),
            """
            namespace Sample.Abstractions;

            public interface IHandler
            {
                void Handle();
            }
            """);
        File.WriteAllText(
            Path.Combine(abstractionsProjectDir, "Abstractions.csproj"),
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
            Path.Combine(appProjectDir, "ConsoleHandler.cs"),
            """
            using Sample.Abstractions;

            namespace Sample.App;

            public sealed class ConsoleHandler : IHandler
            {
                public void Handle()
                {
                }
            }
            """);
        File.WriteAllText(
            Path.Combine(appProjectDir, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Abstractions\Abstractions.csproj" />
              </ItemGroup>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var testsProjectDir = Path.Combine(_tempRoot, "tests", "App.Tests");
        Directory.CreateDirectory(testsProjectDir);
        File.WriteAllText(
            Path.Combine(testsProjectDir, "TestHandler.cs"),
            """
            using Sample.Abstractions;

            namespace Sample.Tests;

            public sealed class TestHandler : IHandler
            {
                public void Handle()
                {
                }
            }
            """);
        File.WriteAllText(
            Path.Combine(testsProjectDir, "App.Tests.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\..\src\Abstractions\Abstractions.csproj" />
              </ItemGroup>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        return WriteSolution(
            """
            <Solution>
              <Project Path="src/Abstractions/Abstractions.csproj" />
              <Project Path="src/App/App.csproj" />
              <Project Path="tests/App.Tests/App.Tests.csproj" />
            </Solution>
            """);
    }

    private string CreateQualifiedResolutionSolution()
    {
        return CreateSingleProjectSolution(
            "App",
            new Dictionary<string, string>
            {
                ["FirstHandler.cs"] = """
                    namespace Sample.First;

                    public interface IHandler
                    {
                        void Handle();
                    }

                    public sealed class FirstHandler : IHandler
                    {
                        public void Handle()
                        {
                        }
                    }
                    """,
                ["SecondHandler.cs"] = """
                    namespace Sample.Second;

                    public interface IHandler
                    {
                        void Handle();
                    }

                    public sealed class SecondHandler : IHandler
                    {
                        public void Handle()
                        {
                        }
                    }
                    """
            });
    }

    private string CreateConcreteClassSolution()
    {
        return CreateSingleProjectSolution(
            "App",
            new Dictionary<string, string>
            {
                ["Worker.cs"] = """
                    namespace Sample.App;

                    public sealed class Worker
                    {
                    }
                    """
            });
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

            public interface SharedType
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

            public abstract class SharedType
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

        return WriteSolution(
            """
            <Solution>
              <Project Path="src/First/First.csproj" />
              <Project Path="src/Second/Second.csproj" />
            </Solution>
            """);
    }

    private string CreateSingleProjectSolution(string projectName, IReadOnlyDictionary<string, string> files)
    {
        Directory.CreateDirectory(_tempRoot);
        var projectDir = Path.Combine(_tempRoot, "src", projectName);
        Directory.CreateDirectory(projectDir);

        foreach (var (fileName, content) in files)
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

        return WriteSolution(
            $"""
            <Solution>
              <Project Path="src/{projectName}/{projectName}.csproj" />
            </Solution>
            """);
    }

    private string WriteSolution(string content)
    {
        var solutionPath = Path.Combine(_tempRoot, "sample.slnx");
        File.WriteAllText(solutionPath, content);
        return solutionPath;
    }
}
