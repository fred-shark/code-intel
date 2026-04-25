using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeIntel.Analysis;
using CodeIntel.Contracts;

namespace CodeIntel.Analysis.Tests;

/// <summary>
/// Проверяет поведение сервиса трассировки цепочки вызовов через Roslyn.
/// </summary>
public sealed class TraceCallersServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "codeintel-trace-callers-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FindAsync_ReturnsDeclarationWithEmptyChains_WhenMethodHasNoCallers()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Service.cs"] = """
                    namespace Sample;

                    public class Service
                    {
                        public void Execute() { }
                    }
                    """
            },
            projectName: "App");
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "Service", "Execute");

        Assert.Equal(solutionPath, response.SolutionPath);
        Assert.Equal("Service", response.Symbol);
        Assert.Equal("Execute", response.Method);
        Assert.NotNull(response.Declaration);
        Assert.Equal("Service", response.Declaration.Symbol);
        Assert.Equal("Execute", response.Declaration.Method);
        Assert.Equal(SymbolKindDto.Class, response.Declaration.Kind);
        Assert.Equal("App", response.Declaration.Project);
        Assert.EndsWith("Service.cs", response.Declaration.FilePath, StringComparison.Ordinal);
        Assert.Empty(response.CallChains);
        Assert.Equal(0, response.EntryPointCount);
    }

    [Fact]
    public async Task FindAsync_ReturnsCallChainWithEntryPoint_WhenMethodIsCalledFromEntryPoint()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Service.cs"] = """
                    namespace Sample;

                    public class Service
                    {
                        public void Execute() { }
                    }
                    """,
                ["Runner.cs"] = """
                    namespace Sample;

                    public class Runner
                    {
                        public void Run()
                        {
                            var service = new Service();
                            service.Execute();
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "Service", "Execute");

        Assert.NotNull(response.Declaration);
        Assert.NotEmpty(response.CallChains);
        Assert.Equal(response.EntryPointCount, CountEntryPoints(response.CallChains));

        var directCaller = response.CallChains.First();
        Assert.Equal("Runner", directCaller.ContainingType);
        Assert.Equal("Run", directCaller.Method);
        Assert.Equal("App", directCaller.Project);
        Assert.EndsWith("Runner.cs", directCaller.FilePath, StringComparison.Ordinal);
        Assert.True(directCaller.IsEntryPoint);
        Assert.Empty(directCaller.CalledBy);
        Assert.NotNull(directCaller.CallSite);
        Assert.EndsWith("Runner.cs", directCaller.CallSite.FilePath, StringComparison.Ordinal);
        Assert.True(directCaller.CallSite.Line > 0);
    }

    [Fact]
    public async Task FindAsync_ExtractsCallCondition_WhenCallIsInsideIfBlock()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Service.cs"] = """
                    namespace Sample;

                    public class Service
                    {
                        public void Execute() { }
                    }
                    """,
                ["Runner.cs"] = """
                    namespace Sample;

                    public class Runner
                    {
                        public void Run(bool enabled)
                        {
                            var service = new Service();
                            if (enabled)
                            {
                                service.Execute();
                            }
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "Service", "Execute");

        var caller = response.CallChains.First(n => n.Method == "Run");
        Assert.Equal("enabled", caller.CallSite.CallCondition);
        Assert.Equal("then", caller.CallSite.Branch);
    }

    [Fact]
    public async Task FindAsync_ExtractsElseBranch_WhenCallIsInsideElseBlock()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Service.cs"] = """
                    namespace Sample;

                    public class Service
                    {
                        public void Execute() { }
                    }
                    """,
                ["Runner.cs"] = """
                    namespace Sample;

                    public class Runner
                    {
                        public void Run(bool skip)
                        {
                            var service = new Service();
                            if (skip)
                            {
                                return;
                            }
                            else
                            {
                                service.Execute();
                            }
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "Service", "Execute");

        var caller = response.CallChains.First(n => n.Method == "Run");
        Assert.Equal("skip", caller.CallSite.CallCondition);
        Assert.Equal("else", caller.CallSite.Branch);
    }

    [Fact]
    public async Task FindAsync_ReturnsNullCondition_WhenCallIsNotInsideIfBlock()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Service.cs"] = """
                    namespace Sample;

                    public class Service
                    {
                        public void Execute() { }
                    }
                    """,
                ["Runner.cs"] = """
                    namespace Sample;

                    public class Runner
                    {
                        public void Run()
                        {
                            var service = new Service();
                            service.Execute();
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "Service", "Execute");

        var caller = response.CallChains.First(n => n.Method == "Run");
        Assert.Null(caller.CallSite.CallCondition);
        Assert.Null(caller.CallSite.Branch);
    }

    [Fact]
    public async Task FindAsync_ReturnsEmptyResponse_WhenTypeIsNotFound()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Service.cs"] = """
                    namespace Sample;

                    public class Service { }
                    """
            },
            projectName: "App");
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "MissingType", "SomeMethod");

        Assert.Null(response.Declaration);
        Assert.Empty(response.CallChains);
        Assert.Equal(0, response.EntryPointCount);
    }

    [Fact]
    public async Task FindAsync_ReturnsDeclarationWithEmptyChains_WhenMethodIsNotFound()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Service.cs"] = """
                    namespace Sample;

                    public class Service
                    {
                        public void Execute() { }
                    }
                    """
            },
            projectName: "App");
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "Service", "NonExistentMethod");

        Assert.NotNull(response.Declaration);
        Assert.Equal("Service", response.Declaration.Symbol);
        Assert.Empty(response.CallChains);
        Assert.Equal(0, response.EntryPointCount);
    }

    [Fact]
    public async Task FindAsync_Throws_WhenSymbolIsAmbiguous()
    {
        var solutionPath = CreateAmbiguousSolution();
        var service = new TraceCallersService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FindAsync(solutionPath, "SharedService", "Execute"));

        Assert.Contains("Multiple matching type declarations were found", exception.Message);
        Assert.Contains("SharedService", exception.Message);
    }

    [Fact]
    public async Task FindAsync_BuildsMultiLevelChain_WhenCallersHaveCallers()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Service.cs"] = """
                    namespace Sample;

                    public class Service
                    {
                        public void Execute() { }
                    }
                    """,
                ["Orchestrator.cs"] = """
                    namespace Sample;

                    public class Orchestrator
                    {
                        private readonly Service _service = new();

                        public void Process()
                        {
                            _service.Execute();
                        }
                    }
                    """,
                ["EntryPoint.cs"] = """
                    namespace Sample;

                    public class EntryPoint
                    {
                        public void Start()
                        {
                            var orchestrator = new Orchestrator();
                            orchestrator.Process();
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "Service", "Execute");

        Assert.NotNull(response.Declaration);
        Assert.NotEmpty(response.CallChains);

        var directCaller = response.CallChains.First(n => n.Method == "Process");
        Assert.Equal("Orchestrator", directCaller.ContainingType);
        Assert.False(directCaller.IsEntryPoint);
        Assert.NotEmpty(directCaller.CalledBy);

        var entryNode = directCaller.CalledBy.First(n => n.Method == "Start");
        Assert.Equal("EntryPoint", entryNode.ContainingType);
        Assert.True(entryNode.IsEntryPoint);
        Assert.True(response.EntryPointCount >= 1);
    }

    [Fact]
    public async Task FindAsync_ExcludesTestProjectCallers_ByDefault()
    {
        var solutionPath = CreateSolutionWithTestProject();
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "Service", "Execute");

        var allNodes = FlattenNodes(response.CallChains);
        Assert.DoesNotContain(allNodes, n => n.Project.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindAsync_IncludesTestProjectCallers_WhenRequested()
    {
        var solutionPath = CreateSolutionWithTestProject();
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "Service", "Execute", includeTests: true);

        var allNodes = FlattenNodes(response.CallChains);
        Assert.Contains(allNodes, n => n.Project.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindAsync_IsCaseInsensitive_ForMethodName()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Service.cs"] = """
                    namespace Sample;

                    public class Service
                    {
                        public void Execute() { }
                    }
                    """
            },
            projectName: "App");
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "Service", "EXECUTE");

        Assert.NotNull(response.Declaration);
        Assert.Equal("Execute", response.Declaration.Method);
    }

    [Fact]
    public async Task FindAsync_ResolvesMetadataType_WhenMethodComesFromFrameworkAssembly()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["BuilderFlow.cs"] = """
                    using System.Text;

                    namespace Sample;

                    public class BuilderFlow
                    {
                        public void Run()
                        {
                            var builder = new StringBuilder();
                            builder.Append("abc");
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new TraceCallersService();

        var response = await service.FindAsync(solutionPath, "System.Text.StringBuilder", "Append");

        Assert.NotNull(response.Declaration);
        Assert.Equal("StringBuilder", response.Declaration.Symbol);
        Assert.Equal("Append", response.Declaration.Method);
        Assert.Equal("global::System.Text.StringBuilder", response.Declaration.FullyQualifiedTypeName);
        Assert.StartsWith("[metadata]/", response.Declaration.FilePath, StringComparison.Ordinal);
        var directCaller = Assert.Single(response.CallChains);
        Assert.Equal("BuilderFlow", directCaller.ContainingType);
        Assert.Equal("Run", directCaller.Method);
        Assert.True(directCaller.IsEntryPoint);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static int CountEntryPoints(IReadOnlyList<CallChainNodeDto> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (node.IsEntryPoint) count++;
            count += CountEntryPoints(node.CalledBy);
        }
        return count;
    }

    private static List<CallChainNodeDto> FlattenNodes(IReadOnlyList<CallChainNodeDto> nodes)
    {
        var result = new List<CallChainNodeDto>();
        foreach (var node in nodes)
        {
            result.Add(node);
            result.AddRange(FlattenNodes(node.CalledBy));
        }
        return result;
    }

    private string CreateAmbiguousSolution()
    {
        Directory.CreateDirectory(_tempRoot);

        var firstProjectDir = Path.Combine(_tempRoot, "src", "First");
        Directory.CreateDirectory(firstProjectDir);
        File.WriteAllText(Path.Combine(firstProjectDir, "SharedService.cs"), """
            namespace Sample.First;

            public class SharedService
            {
                public void Execute() { }
            }
            """);
        File.WriteAllText(Path.Combine(firstProjectDir, "First.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var secondProjectDir = Path.Combine(_tempRoot, "src", "Second");
        Directory.CreateDirectory(secondProjectDir);
        File.WriteAllText(Path.Combine(secondProjectDir, "SharedService.cs"), """
            namespace Sample.Second;

            public class SharedService
            {
                public void Execute() { }
            }
            """);
        File.WriteAllText(Path.Combine(secondProjectDir, "Second.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var solutionPath = Path.Combine(_tempRoot, "sample.slnx");
        File.WriteAllText(solutionPath, """
            <Solution>
              <Project Path="src/First/First.csproj" />
              <Project Path="src/Second/Second.csproj" />
            </Solution>
            """);

        return solutionPath;
    }

    private string CreateSolutionWithTestProject()
    {
        Directory.CreateDirectory(_tempRoot);

        var appDir = Path.Combine(_tempRoot, "src", "App");
        Directory.CreateDirectory(appDir);
        File.WriteAllText(Path.Combine(appDir, "Service.cs"), """
            namespace Sample;

            public class Service
            {
                public void Execute() { }
            }
            """);
        File.WriteAllText(Path.Combine(appDir, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var testsDir = Path.Combine(_tempRoot, "src", "App.Tests");
        Directory.CreateDirectory(testsDir);
        File.WriteAllText(Path.Combine(testsDir, "ServiceTests.cs"), """
            using Sample;

            namespace App.Tests;

            public class ServiceTests
            {
                public void Test_Execute()
                {
                    var service = new Service();
                    service.Execute();
                }
            }
            """);
        File.WriteAllText(Path.Combine(testsDir, "App.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../App/App.csproj" />
              </ItemGroup>
            </Project>
            """);

        var solutionPath = Path.Combine(_tempRoot, "sample.slnx");
        File.WriteAllText(solutionPath, """
            <Solution>
              <Project Path="src/App/App.csproj" />
              <Project Path="src/App.Tests/App.Tests.csproj" />
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
