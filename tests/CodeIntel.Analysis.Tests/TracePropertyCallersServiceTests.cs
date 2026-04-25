using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeIntel.Analysis;
using CodeIntel.Contracts;

namespace CodeIntel.Analysis.Tests;

/// <summary>
/// Проверяет поведение сервиса трассировки чтений и записей свойства через Roslyn.
/// </summary>
public sealed class TracePropertyCallersServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "codeintel-trace-property-callers-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FindAsync_ReturnsSetChain_ForObjectInitializerAssignment()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Response.cs"] = """
                    namespace Sample;

                    public class Response
                    {
                        public string SolutionPath { get; set; } = string.Empty;
                    }
                    """,
                ["Builder.cs"] = """
                    namespace Sample;

                    public class Builder
                    {
                        public Response Create(string path, bool enabled)
                        {
                            if (enabled)
                            {
                                return new Response
                                {
                                    SolutionPath = path
                                };
                            }

                            return new Response();
                        }
                    }
                    """,
                ["Endpoint.cs"] = """
                    namespace Sample;

                    public class Endpoint
                    {
                        public Response Handle(string path)
                        {
                            var builder = new Builder();
                            return builder.Create(path, enabled: true);
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new TracePropertyCallersService();

        var response = await service.FindAsync(solutionPath, "Response", "SolutionPath", PropertyAccessKindDto.Set);

        Assert.NotNull(response.Declaration);
        Assert.Equal("SolutionPath", response.Declaration.Property);
        Assert.True(response.Declaration.HasGetter);
        Assert.True(response.Declaration.HasSetter);

        var setChains = Assert.Single(response.AccessChains);
        Assert.Equal(PropertyAccessKindDto.Set, setChains.Access);
        var directCaller = Assert.Single(setChains.CallChains);
        Assert.Equal("Builder", directCaller.ContainingType);
        Assert.Equal("Create", directCaller.Method);
        Assert.Equal("enabled", directCaller.CallSite.CallCondition);
        Assert.Equal("then", directCaller.CallSite.Branch);
        var entryPoint = Assert.Single(directCaller.CalledBy);
        Assert.Equal("Handle", entryPoint.Method);
        Assert.True(entryPoint.IsEntryPoint);
    }

    [Fact]
    public async Task FindAsync_ReturnsGetChain_ForPropertyRead()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Response.cs"] = """
                    namespace Sample;

                    public class Response
                    {
                        public string SolutionPath { get; set; } = string.Empty;
                    }
                    """,
                ["Formatter.cs"] = """
                    namespace Sample;

                    public class Formatter
                    {
                        public string Format(Response response, bool enabled)
                        {
                            if (enabled)
                            {
                                return response.SolutionPath;
                            }

                            return string.Empty;
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new TracePropertyCallersService();

        var response = await service.FindAsync(solutionPath, "Response", "SolutionPath", PropertyAccessKindDto.Get);

        var getChains = Assert.Single(response.AccessChains);
        var directCaller = Assert.Single(getChains.CallChains);
        Assert.Equal("Formatter", directCaller.ContainingType);
        Assert.Equal("Format", directCaller.Method);
        Assert.Equal("enabled", directCaller.CallSite.CallCondition);
        Assert.Equal("then", directCaller.CallSite.Branch);
        Assert.True(directCaller.IsEntryPoint);
    }

    [Fact]
    public async Task FindAsync_ReturnsBothAccessBuckets_ForCompoundAssignment()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Counter.cs"] = """
                    namespace Sample;

                    public class Counter
                    {
                        public int Count { get; set; }
                    }
                    """,
                ["Runner.cs"] = """
                    namespace Sample;

                    public class Runner
                    {
                        public void Increment(Counter counter)
                        {
                            counter.Count += 1;
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new TracePropertyCallersService();

        var response = await service.FindAsync(solutionPath, "Counter", "Count", PropertyAccessKindDto.Both);

        Assert.Equal(2, response.AccessChains.Count);
        var getChains = Assert.Single(response.AccessChains, chain => chain.Access == PropertyAccessKindDto.Get);
        var setChains = Assert.Single(response.AccessChains, chain => chain.Access == PropertyAccessKindDto.Set);
        Assert.Single(getChains.CallChains);
        Assert.Single(setChains.CallChains);
        Assert.Equal("Increment", getChains.CallChains[0].Method);
        Assert.Equal("Increment", setChains.CallChains[0].Method);
    }

    [Fact]
    public async Task FindAsync_ExcludesTestProjectUsages_ByDefault()
    {
        var solutionPath = CreateSolutionWithTestProject();
        var service = new TracePropertyCallersService();

        var response = await service.FindAsync(solutionPath, "Response", "SolutionPath", PropertyAccessKindDto.Get);

        var nodes = FlattenNodes(response.AccessChains.SelectMany(chain => chain.CallChains).ToArray());
        Assert.DoesNotContain(nodes, node => node.Project.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindAsync_IncludesTestProjectUsages_WhenRequested()
    {
        var solutionPath = CreateSolutionWithTestProject();
        var service = new TracePropertyCallersService();

        var response = await service.FindAsync(solutionPath, "Response", "SolutionPath", PropertyAccessKindDto.Get, includeTests: true);

        var nodes = FlattenNodes(response.AccessChains.SelectMany(chain => chain.CallChains).ToArray());
        Assert.Contains(nodes, node => node.Project.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindAsync_ReturnsEmptyChains_WhenRequestedAccessorIsMissing()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["Response.cs"] = """
                    namespace Sample;

                    public class Response
                    {
                        public string SolutionPath { get; } = string.Empty;
                    }
                    """
            },
            projectName: "App");
        var service = new TracePropertyCallersService();

        var response = await service.FindAsync(solutionPath, "Response", "SolutionPath", PropertyAccessKindDto.Set);

        Assert.NotNull(response.Declaration);
        Assert.True(response.Declaration.HasGetter);
        Assert.False(response.Declaration.HasSetter);
        Assert.Empty(response.AccessChains);
    }

    [Fact]
    public async Task FindAsync_ResolvesMetadataType_WhenPropertyComesFromFrameworkAssembly()
    {
        var solutionPath = CreateSolution(
            new Dictionary<string, string>
            {
                ["BuilderFlow.cs"] = """
                    using System.Text;

                    namespace Sample;

                    public class BuilderFlow
                    {
                        public int Run()
                        {
                            var builder = new StringBuilder();
                            builder.Append("abc");
                            return builder.Length;
                        }
                    }
                    """
            },
            projectName: "App");
        var service = new TracePropertyCallersService();

        var response = await service.FindAsync(solutionPath, "System.Text.StringBuilder", "Length", PropertyAccessKindDto.Get);

        Assert.NotNull(response.Declaration);
        Assert.Equal("StringBuilder", response.Declaration.Symbol);
        Assert.Equal("Length", response.Declaration.Property);
        Assert.Equal("global::System.Text.StringBuilder", response.Declaration.FullyQualifiedTypeName);
        Assert.StartsWith("[metadata]/", response.Declaration.FilePath, StringComparison.Ordinal);
        var getChains = Assert.Single(response.AccessChains);
        var directCaller = Assert.Single(getChains.CallChains);
        Assert.Equal("BuilderFlow", directCaller.ContainingType);
        Assert.Equal("Run", directCaller.Method);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
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

    private string CreateSolutionWithTestProject()
    {
        Directory.CreateDirectory(_tempRoot);

        var appDir = Path.Combine(_tempRoot, "src", "App");
        Directory.CreateDirectory(appDir);
        File.WriteAllText(Path.Combine(appDir, "Response.cs"), """
            namespace Sample;

            public class Response
            {
                public string SolutionPath { get; set; } = string.Empty;
            }
            """);
        File.WriteAllText(Path.Combine(appDir, "Reader.cs"), """
            namespace Sample;

            public class Reader
            {
                public string Read(Response response)
                {
                    return response.SolutionPath;
                }
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
        File.WriteAllText(Path.Combine(testsDir, "ResponseTests.cs"), """
            using Sample;

            namespace App.Tests;

            public class ResponseTests
            {
                public string Read(Response response)
                {
                    return response.SolutionPath;
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
