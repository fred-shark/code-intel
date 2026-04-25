using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Analysis;
using CodeIntel.Contracts;

namespace CodeIntel.Analysis.Tests;

/// <summary>
/// Проверяет, как сервис анализа влияния учитывает фильтрацию тестовых проектов.
/// </summary>
public sealed class AnalyzeImpactServiceTests
{
    /// <summary>
    /// Проверяет, что анализ влияния по умолчанию исключает реализации из тестовых проектов.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ExcludesTestImplementations_ByDefault()
    {
        var service = new AnalyzeImpactService(
            new StubFindReferencesService(
                new FindReferencesResponseDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Symbol = "IHandler",
                    Declaration = new FindReferencesDeclarationDto
                    {
                        Symbol = "IHandler",
                        FullyQualifiedName = "global::Sample.IHandler",
                        Kind = SymbolKindDto.Interface,
                        Project = "Abstractions",
                        FilePath = "/repo/src/Abstractions/IHandler.cs",
                        Line = 3,
                        Column = 18
                    },
                    References = [],
                    ReferenceCount = 0
                }),
            new StubFindImplementationsService(
                new FindImplementationsResponseDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Symbol = "IHandler",
                    Declaration = new FindImplementationsDeclarationDto
                    {
                        Symbol = "IHandler",
                        FullyQualifiedName = "global::Sample.IHandler",
                        Kind = SymbolKindDto.Interface,
                        Project = "Abstractions",
                        FilePath = "/repo/src/Abstractions/IHandler.cs",
                        Line = 3,
                        Column = 18
                    },
                    Implementations =
                    [
                        new FindImplementationsResultDto
                        {
                            Symbol = "ConsoleHandler",
                            FullyQualifiedName = "global::Sample.ConsoleHandler",
                            Kind = SymbolKindDto.Class,
                            Project = "App",
                            FilePath = "/repo/src/App/ConsoleHandler.cs",
                            Line = 5,
                            Column = 21
                        }
                    ],
                    ImplementationCount = 1
                },
                new FindImplementationsResponseDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Symbol = "IHandler",
                    Declaration = new FindImplementationsDeclarationDto
                    {
                        Symbol = "IHandler",
                        FullyQualifiedName = "global::Sample.IHandler",
                        Kind = SymbolKindDto.Interface,
                        Project = "Abstractions",
                        FilePath = "/repo/src/Abstractions/IHandler.cs",
                        Line = 3,
                        Column = 18
                    },
                    Implementations =
                    [
                        new FindImplementationsResultDto
                        {
                            Symbol = "ConsoleHandler",
                            FullyQualifiedName = "global::Sample.ConsoleHandler",
                            Kind = SymbolKindDto.Class,
                            Project = "App",
                            FilePath = "/repo/src/App/ConsoleHandler.cs",
                            Line = 5,
                            Column = 21
                        },
                        new FindImplementationsResultDto
                        {
                            Symbol = "TestHandler",
                            FullyQualifiedName = "global::Sample.Tests.TestHandler",
                            Kind = SymbolKindDto.Class,
                            Project = "App.Tests",
                            FilePath = "/repo/tests/App.Tests/TestHandler.cs",
                            Line = 5,
                            Column = 21
                        }
                    ],
                    ImplementationCount = 2
                }));

        var response = await service.AnalyzeAsync("/repo/sample.slnx", "IHandler");

        Assert.Equal(1, response.ImplementationCount);
        Assert.Equal(["Abstractions", "App"], response.AffectedProjects);
    }

    /// <summary>
    /// Проверяет, что анализ влияния по умолчанию исключает ссылки из тестовых проектов.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ExcludesTestReferences_ByDefault()
    {
        var service = new AnalyzeImpactService(
            new StubFindReferencesService(
                new FindReferencesResponseDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Symbol = "IHandler",
                    Declaration = new FindReferencesDeclarationDto
                    {
                        Symbol = "IHandler",
                        FullyQualifiedName = "global::Sample.IHandler",
                        Kind = SymbolKindDto.Interface,
                        Project = "Abstractions",
                        FilePath = "/repo/src/Abstractions/IHandler.cs",
                        Line = 3,
                        Column = 18
                    },
                    References =
                    [
                        new FindReferencesResultDto
                        {
                            Symbol = "IHandler",
                            ReferencedSymbolFullyQualifiedName = "global::Sample.IHandler",
                            Project = "App",
                            FilePath = "/repo/src/App/ConsoleHandler.cs",
                            Line = 5,
                            Column = 21
                        },
                        new FindReferencesResultDto
                        {
                            Symbol = "IHandler",
                            ReferencedSymbolFullyQualifiedName = "global::Sample.IHandler",
                            Project = "App.Tests",
                            FilePath = "/repo/tests/App.Tests/ConsoleHandlerTests.cs",
                            Line = 7,
                            Column = 13
                        }
                    ],
                    ReferenceCount = 2
                }),
            new StubFindImplementationsService(CreateEmptyImplementationsResponse(), CreateEmptyImplementationsResponse()));

        var response = await service.AnalyzeAsync("/repo/sample.slnx", "IHandler");

        Assert.Equal(1, response.ReferenceCount);
        Assert.Equal(["Abstractions", "App"], response.AffectedProjects);
    }

    /// <summary>
    /// Проверяет, что анализ влияния включает тестовые проекты при явном запросе.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_IncludesTestImplementations_WhenRequested()
    {
        var service = new AnalyzeImpactService(
            new StubFindReferencesService(
                new FindReferencesResponseDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Symbol = "IHandler",
                    Declaration = new FindReferencesDeclarationDto
                    {
                        Symbol = "IHandler",
                        FullyQualifiedName = "global::Sample.IHandler",
                        Kind = SymbolKindDto.Interface,
                        Project = "Abstractions",
                        FilePath = "/repo/src/Abstractions/IHandler.cs",
                        Line = 3,
                        Column = 18
                    },
                    References = [],
                    ReferenceCount = 0
                }),
            new StubFindImplementationsService(
                filteredResponse: new FindImplementationsResponseDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Symbol = "IHandler",
                    Declaration = null,
                    Implementations = [],
                    ImplementationCount = 0
                },
                includeTestsResponse: new FindImplementationsResponseDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Symbol = "IHandler",
                    Declaration = new FindImplementationsDeclarationDto
                    {
                        Symbol = "IHandler",
                        FullyQualifiedName = "global::Sample.IHandler",
                        Kind = SymbolKindDto.Interface,
                        Project = "Abstractions",
                        FilePath = "/repo/src/Abstractions/IHandler.cs",
                        Line = 3,
                        Column = 18
                    },
                    Implementations =
                    [
                        new FindImplementationsResultDto
                        {
                            Symbol = "ConsoleHandler",
                            FullyQualifiedName = "global::Sample.ConsoleHandler",
                            Kind = SymbolKindDto.Class,
                            Project = "App",
                            FilePath = "/repo/src/App/ConsoleHandler.cs",
                            Line = 5,
                            Column = 21
                        },
                        new FindImplementationsResultDto
                        {
                            Symbol = "TestHandler",
                            FullyQualifiedName = "global::Sample.Tests.TestHandler",
                            Kind = SymbolKindDto.Class,
                            Project = "App.Tests",
                            FilePath = "/repo/tests/App.Tests/TestHandler.cs",
                            Line = 5,
                            Column = 21
                        }
                    ],
                    ImplementationCount = 2
                }));

        var response = await service.AnalyzeAsync("/repo/sample.slnx", "IHandler", includeTests: true);

        Assert.Equal(2, response.ImplementationCount);
        Assert.Equal(["Abstractions", "App", "App.Tests"], response.AffectedProjects);
    }

    /// <summary>
    /// Проверяет, что анализ влияния возвращает полный счетчик ссылок при включении тестовых проектов.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_IncludesTestReferences_WhenRequested()
    {
        var referencesResponse = new FindReferencesResponseDto
        {
            SolutionPath = "/repo/sample.slnx",
            Symbol = "IHandler",
            Declaration = new FindReferencesDeclarationDto
            {
                Symbol = "IHandler",
                FullyQualifiedName = "global::Sample.IHandler",
                Kind = SymbolKindDto.Interface,
                Project = "Abstractions",
                FilePath = "/repo/src/Abstractions/IHandler.cs",
                Line = 3,
                Column = 18
            },
            References =
            [
                new FindReferencesResultDto
                {
                    Symbol = "IHandler",
                    ReferencedSymbolFullyQualifiedName = "global::Sample.IHandler",
                    Project = "App",
                    FilePath = "/repo/src/App/ConsoleHandler.cs",
                    Line = 5,
                    Column = 21
                },
                new FindReferencesResultDto
                {
                    Symbol = "IHandler",
                    ReferencedSymbolFullyQualifiedName = "global::Sample.IHandler",
                    Project = "App.Tests",
                    FilePath = "/repo/tests/App.Tests/ConsoleHandlerTests.cs",
                    Line = 7,
                    Column = 13
                }
            ],
            ReferenceCount = 2
        };
        var service = new AnalyzeImpactService(
            new StubFindReferencesService(referencesResponse),
            new StubFindImplementationsService(CreateEmptyImplementationsResponse(), CreateEmptyImplementationsResponse()));

        var response = await service.AnalyzeAsync("/repo/sample.slnx", "IHandler", includeTests: true);

        Assert.Equal(2, response.ReferenceCount);
        Assert.Equal(["Abstractions", "App", "App.Tests"], response.AffectedProjects);
    }

    /// <summary>
    /// Проверяет, что уровень риска меняется, когда тестовые ссылки исключаются из расчета.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ChangesRiskSummary_WhenTestReferencesAreFiltered()
    {
        var referencesResponse = new FindReferencesResponseDto
        {
            SolutionPath = "/repo/sample.slnx",
            Symbol = "IHandler",
            Declaration = new FindReferencesDeclarationDto
            {
                Symbol = "IHandler",
                FullyQualifiedName = "global::Sample.IHandler",
                Kind = SymbolKindDto.Interface,
                Project = "Abstractions",
                FilePath = "/repo/src/Abstractions/IHandler.cs",
                Line = 3,
                Column = 18
            },
            References =
            [
                new FindReferencesResultDto
                {
                    Symbol = "IHandler",
                    ReferencedSymbolFullyQualifiedName = "global::Sample.IHandler",
                    Project = "App",
                    FilePath = "/repo/src/App/ConsoleHandler.cs",
                    Line = 5,
                    Column = 21
                },
                new FindReferencesResultDto
                {
                    Symbol = "IHandler",
                    ReferencedSymbolFullyQualifiedName = "global::Sample.IHandler",
                    Project = "App.Tests",
                    FilePath = "/repo/tests/App.Tests/ConsoleHandlerTests.cs",
                    Line = 7,
                    Column = 13
                }
            ],
            ReferenceCount = 2
        };
        var implementationsResponse = new FindImplementationsResponseDto
        {
            SolutionPath = "/repo/sample.slnx",
            Symbol = "IHandler",
            Declaration = new FindImplementationsDeclarationDto
            {
                Symbol = "IHandler",
                FullyQualifiedName = "global::Sample.IHandler",
                Kind = SymbolKindDto.Interface,
                Project = "Abstractions",
                FilePath = "/repo/src/Abstractions/IHandler.cs",
                Line = 3,
                Column = 18
            },
            Implementations = [],
            ImplementationCount = 0
        };
        var service = new AnalyzeImpactService(
            new StubFindReferencesService(referencesResponse),
            new StubFindImplementationsService(implementationsResponse, implementationsResponse));

        var filteredResponse = await service.AnalyzeAsync("/repo/sample.slnx", "IHandler");
        var fullResponse = await service.AnalyzeAsync("/repo/sample.slnx", "IHandler", includeTests: true);

        Assert.Equal("Medium", filteredResponse.RiskSummary);
        Assert.Equal("High", fullResponse.RiskSummary);
    }

    private static FindImplementationsResponseDto CreateEmptyImplementationsResponse()
    {
        return new FindImplementationsResponseDto
        {
            SolutionPath = "/repo/sample.slnx",
            Symbol = "IHandler",
            Declaration = new FindImplementationsDeclarationDto
            {
                Symbol = "IHandler",
                FullyQualifiedName = "global::Sample.IHandler",
                Kind = SymbolKindDto.Interface,
                Project = "Abstractions",
                FilePath = "/repo/src/Abstractions/IHandler.cs",
                Line = 3,
                Column = 18
            },
            Implementations = [],
            ImplementationCount = 0
        };
    }

    private sealed class StubFindReferencesService(FindReferencesResponseDto response) : IFindReferencesService
    {
        public Task<FindReferencesResponseDto> FindAsync(string solutionPath, string symbol, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(response);
        }
    }

    private sealed class StubFindImplementationsService(
        FindImplementationsResponseDto filteredResponse,
        FindImplementationsResponseDto includeTestsResponse) : IFindImplementationsService
    {
        public Task<FindImplementationsResponseDto> FindAsync(
            string solutionPath,
            string symbol,
            bool includeTests = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(includeTests ? includeTestsResponse : filteredResponse);
        }
    }
}

/// <summary>
/// Проверяет разрешение fully qualified имен в реальном analyze-impact сценарии.
/// </summary>
public sealed class AnalyzeImpactQualifiedResolutionIntegrationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "codeintel-analyze-impact-qualified-tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Проверяет, что fully qualified запрос выбирает правильный тип среди одноименных интерфейсов.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ReturnsImpact_ForFullyQualifiedSymbol()
    {
        var solutionPath = CreateQualifiedResolutionSolution();
        var service = new AnalyzeImpactService(new FindReferencesService(), new FindImplementationsService());

        var response = await service.AnalyzeAsync(solutionPath, "Sample.Second.IHandler");

        Assert.NotNull(response.Declaration);
        Assert.Equal("global::Sample.Second.IHandler", response.Declaration.FullyQualifiedName);
        Assert.Equal(3, response.ReferenceCount);
        Assert.Equal(1, response.ImplementationCount);
        Assert.Equal(["App"], response.AffectedProjects);
    }

    /// <summary>
    /// Проверяет поддержку fully qualified имени с префиксом global:: в analyze-impact.
    /// </summary>
    [Fact]
    public async Task AnalyzeAsync_ReturnsImpact_ForGlobalQualifiedSymbol()
    {
        var solutionPath = CreateQualifiedResolutionSolution();
        var service = new AnalyzeImpactService(new FindReferencesService(), new FindImplementationsService());

        var response = await service.AnalyzeAsync(solutionPath, "global::Sample.Second.IHandler");

        Assert.NotNull(response.Declaration);
        Assert.Equal("global::Sample.Second.IHandler", response.Declaration.FullyQualifiedName);
        Assert.Equal(1, response.ImplementationCount);
    }

    /// <summary>
    /// Удаляет временные файлы интеграционного теста.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string CreateQualifiedResolutionSolution()
    {
        Directory.CreateDirectory(_tempRoot);

        var projectDir = Path.Combine(_tempRoot, "src", "App");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(
            Path.Combine(projectDir, "Handlers.cs"),
            """
            namespace Sample.First
            {
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
            }

            namespace Sample.Second
            {
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
            }
            """);
        File.WriteAllText(
            Path.Combine(projectDir, "Consumers.cs"),
            """
            namespace Sample.App
            {
                public sealed class UsesSecondHandler
                {
                    private readonly Sample.Second.IHandler _handler;

                    public UsesSecondHandler(Sample.Second.IHandler handler)
                    {
                        _handler = handler;
                    }
                }
            }
            """);
        File.WriteAllText(
            Path.Combine(projectDir, "App.csproj"),
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
              <Project Path="src/App/App.csproj" />
            </Solution>
            """);

        return solutionPath;
    }
}
