using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Analysis;
using CodeIntel.Cli;
using CodeIntel.Contracts;
using CodeIntel.Loader;

namespace CodeIntel.Cli.Tests;

/// <summary>
/// Проверяет поведение CLI-оболочки для поддерживаемых команд.
/// </summary>
public sealed class CliApplicationTests
{
    [Fact]
    public async Task RunAsync_WritesSolutionSummaryJson_ForPositionalSolutionArgument()
    {
        var app = CreateSolutionSummaryApp(
            new SolutionSummaryDto
            {
                SolutionPath = "/repo/sample.slnx",
                Projects =
                [
                    new ProjectSummaryDto
                    {
                        Name = "App",
                        Path = "/repo/src/App/App.csproj",
                        TargetFrameworks = ["net10.0"],
                        IsTestProject = false
                    }
                ]
            });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(["solution-summary", "sample.slnx"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("\"solutionPath\": \"/repo/sample.slnx\"", output.ToString());
        Assert.Contains("\"name\": \"App\"", output.ToString());
        Assert.Contains("\"targetFrameworks\": [", output.ToString());
    }

    [Fact]
    public async Task RunAsync_WritesSolutionSummaryJson_ForExplicitSolutionOption()
    {
        var app = CreateSolutionSummaryApp(
            new SolutionSummaryDto
            {
                SolutionPath = "/repo/sample.slnx",
                Projects = []
            });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(["solution-summary", "--solution", "sample.slnx"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("\"solutionPath\": \"/repo/sample.slnx\"", output.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsUsageForUnknownArguments()
    {
        var app = CreateSolutionSummaryApp(
            new SolutionSummaryDto
            {
                SolutionPath = "/repo/sample.slnx",
                Projects = []
            });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync([], output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Usage: codeintel solution-summary", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForInvalidSolutionPath()
    {
        var app = new CliApplication(
            new ThrowingSolutionSummaryLoader(
                new FileNotFoundException("Solution file was not found: /missing/sample.slnx", "/missing/sample.slnx")),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(["solution-summary", "--solution", "/missing/sample.slnx"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Solution file was not found", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForMissingSolutionOptionValue()
    {
        var app = CreateSolutionSummaryApp(
            new SolutionSummaryDto
            {
                SolutionPath = "/repo/sample.slnx",
                Projects = []
            });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(["solution-summary", "--solution"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Missing value for --solution.", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForJsonOptionBecauseJsonIsDefault()
    {
        var app = CreateSolutionSummaryApp(
            new SolutionSummaryDto
            {
                SolutionPath = "/repo/sample.slnx",
                Projects = []
            });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(["solution-summary", "--solution", "sample.slnx", "--json"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Unknown option: --json", error.ToString());
    }

    [Fact]
    public async Task RunAsync_WritesFindSymbolJson_ForValidArguments()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(
                new FindSymbolResponseDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Name = "Target",
                    Results =
                    [
                        new FindSymbolResultDto
                        {
                            Name = "Target",
                            FullyQualifiedName = "global::Sample.Target",
                            Kind = SymbolKindDto.Class,
                            Project = "App",
                            FilePath = "/repo/src/App/Target.cs",
                            Line = 10,
                            Column = 5
                        }
                    ]
                }),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-symbol", "--solution", "sample.slnx", "--name", "Target"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var payload = output.ToString();
        Assert.Contains("\"solutionPath\": \"/repo/sample.slnx\"", payload);
        Assert.Contains("\"name\": \"Target\"", payload);
        Assert.Contains("\"kind\": \"Class\"", payload);
        Assert.Contains("\"line\": 10", payload);
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForMissingNameOption()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-symbol", "--solution", "sample.slnx"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Missing required --name option.", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForUnknownFindSymbolOption()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-symbol", "--solution", "sample.slnx", "--name", "Target", "--json"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Unknown option: --json", error.ToString());
    }

    [Fact]
    public async Task RunAsync_WritesFindReferencesJson_ForValidArguments()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(
                new FindReferencesResponseDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Symbol = "Target",
                    Declaration = new FindReferencesDeclarationDto
                    {
                        Symbol = "Target",
                        FullyQualifiedName = "global::Sample.Target",
                        Kind = SymbolKindDto.Class,
                        Project = "App",
                        FilePath = "/repo/src/App/Target.cs",
                        Line = 4,
                        Column = 18
                    },
                    References =
                    [
                        new FindReferencesResultDto
                        {
                            Symbol = "Target",
                            ReferencedSymbolFullyQualifiedName = "global::Sample.Target",
                            Project = "App",
                            FilePath = "/repo/src/App/Consumer.cs",
                            Line = 10,
                            Column = 12
                        }
                    ],
                    ReferenceCount = 1
                }),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-references", "--solution", "sample.slnx", "--symbol", "Target"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var payload = output.ToString();
        Assert.Contains("\"symbol\": \"Target\"", payload);
        Assert.Contains("\"declaration\": {", payload);
        Assert.Contains("\"referencedSymbolFullyQualifiedName\": \"global::Sample.Target\"", payload);
        Assert.Contains("\"referenceCount\": 1", payload);
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForMissingSymbolOption_ForFindReferences()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-references", "--solution", "sample.slnx"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Missing required --symbol option.", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForAmbiguousFindReferencesSymbol()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new ThrowingFindReferencesService(new InvalidOperationException("Multiple matching type declarations were found for symbol 'Target'.")),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-references", "--solution", "sample.slnx", "--symbol", "Target"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Multiple matching type declarations were found", error.ToString());
    }

    [Fact]
    public async Task RunAsync_WritesFindImplementationsJson_ForValidArguments()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
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
                }),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-implementations", "--solution", "sample.slnx", "--symbol", "IHandler"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var payload = output.ToString();
        Assert.Contains("\"symbol\": \"IHandler\"", payload);
        Assert.Contains("\"implementations\": [", payload);
        Assert.Contains("\"fullyQualifiedName\": \"global::Sample.ConsoleHandler\"", payload);
        Assert.Contains("\"implementationCount\": 1", payload);
    }

    /// <summary>
    /// Проверяет, что CLI принимает флаг включения тестовых проектов для поиска реализаций.
    /// </summary>
    [Fact]
    public async Task RunAsync_AllowsIncludeTestsFlag_ForFindImplementations()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-implementations", "--solution", "sample.slnx", "--symbol", "IHandler", "--include-tests"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForMissingSymbolOption_ForFindImplementations()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-implementations", "--solution", "sample.slnx"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Missing required --symbol option.", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForAmbiguousFindImplementationsSymbol()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new ThrowingFindImplementationsService(new InvalidOperationException("Multiple matching type declarations were found for symbol 'Target'.")),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-implementations", "--solution", "sample.slnx", "--symbol", "Target"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Multiple matching type declarations were found", error.ToString());
    }

    [Fact]
    public async Task RunAsync_WritesAnalyzeImpactJson_ForValidArguments()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(
                new AnalyzeImpactResponseDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Symbol = "Widget",
                    Declaration = new AnalyzeImpactDeclarationDto
                    {
                        Symbol = "Widget",
                        FullyQualifiedName = "global::Sample.Widget",
                        Kind = SymbolKindDto.Class,
                        Project = "Domain",
                        FilePath = "/repo/src/Domain/Widget.cs",
                        Line = 4,
                        Column = 21
                    },
                    ReferenceCount = 3,
                    ImplementationCount = 0,
                    AffectedProjects = ["App", "Domain", "Tests"],
                    RiskSummary = "High"
                }));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["analyze-impact", "--solution", "sample.slnx", "--symbol", "Widget"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var payload = output.ToString();
        Assert.Contains("\"symbol\": \"Widget\"", payload);
        Assert.Contains("\"affectedProjects\": [", payload);
        Assert.Contains("\"riskSummary\": \"High\"", payload);
        Assert.Contains("\"referenceCount\": 3", payload);
    }

    /// <summary>
    /// Проверяет, что CLI принимает флаг включения тестовых проектов для анализа влияния.
    /// </summary>
    [Fact]
    public async Task RunAsync_AllowsIncludeTestsFlag_ForAnalyzeImpact()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["analyze-impact", "--solution", "sample.slnx", "--symbol", "Widget", "--include-tests"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForMissingSymbolOption_ForAnalyzeImpact()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["analyze-impact", "--solution", "sample.slnx"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Missing required --symbol option.", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsUnknownJson_ForAnalyzeImpactWhenSymbolIsNotFound()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["analyze-impact", "--solution", "sample.slnx", "--symbol", "MissingType"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var payload = output.ToString();
        Assert.Contains("\"declaration\": null", payload);
        Assert.Contains("\"riskSummary\": \"Unknown\"", payload);
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForAmbiguousAnalyzeImpactSymbol()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = []
                }),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new ThrowingAnalyzeImpactService(new InvalidOperationException("Multiple matching type declarations were found for symbol 'Target'.")));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["analyze-impact", "--solution", "sample.slnx", "--symbol", "Target"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Multiple matching type declarations were found", error.ToString());
    }

    private static CliApplication CreateSolutionSummaryApp(SolutionSummaryDto summary)
    {
        return new CliApplication(
            new StubSolutionSummaryLoader(summary),
            new StubFindSymbolService(CreateEmptyFindSymbolResponse()),
            new StubFindReferencesService(CreateEmptyFindReferencesResponse()),
            new StubFindImplementationsService(CreateEmptyFindImplementationsResponse()),
            new StubAnalyzeImpactService(CreateEmptyAnalyzeImpactResponse()));
    }

    private static FindSymbolResponseDto CreateEmptyFindSymbolResponse()
    {
        return new FindSymbolResponseDto
        {
            SolutionPath = string.Empty,
            Name = string.Empty,
            Results = Array.Empty<FindSymbolResultDto>()
        };
    }

    private static FindReferencesResponseDto CreateEmptyFindReferencesResponse()
    {
        return new FindReferencesResponseDto
        {
            SolutionPath = string.Empty,
            Symbol = string.Empty,
            Declaration = null,
            References = Array.Empty<FindReferencesResultDto>(),
            ReferenceCount = 0
        };
    }

    private static FindImplementationsResponseDto CreateEmptyFindImplementationsResponse()
    {
        return new FindImplementationsResponseDto
        {
            SolutionPath = string.Empty,
            Symbol = string.Empty,
            Declaration = null,
            Implementations = Array.Empty<FindImplementationsResultDto>(),
            ImplementationCount = 0
        };
    }

    private static AnalyzeImpactResponseDto CreateEmptyAnalyzeImpactResponse()
    {
        return new AnalyzeImpactResponseDto
        {
            SolutionPath = string.Empty,
            Symbol = string.Empty,
            Declaration = null,
            ReferenceCount = 0,
            ImplementationCount = 0,
            AffectedProjects = Array.Empty<string>(),
            RiskSummary = "Unknown"
        };
    }

    /// <summary>
    /// Возвращает заранее подготовленную сводку решения для CLI-тестов.
    /// </summary>
    private sealed class StubSolutionSummaryLoader(SolutionSummaryDto summary) : ISolutionSummaryLoader
    {
        /// <summary>
        /// Возвращает подготовленную сводку без чтения файловой системы.
        /// </summary>
        /// <param name="solutionPath">Путь к решению.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Подготовленная сводка.</returns>
        public Task<SolutionSummaryDto> LoadAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(summary);
        }
    }

    private sealed class StubFindSymbolService(FindSymbolResponseDto response) : IFindSymbolService
    {
        public Task<FindSymbolResponseDto> FindAsync(string solutionPath, string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Возвращает заранее подготовленный ответ поиска ссылок без обращения к Roslyn.
    /// </summary>
    private sealed class StubFindReferencesService(FindReferencesResponseDto response) : IFindReferencesService
    {
        /// <summary>
        /// Возвращает подготовленный ответ поиска ссылок.
        /// </summary>
        /// <param name="solutionPath">Путь к решению.</param>
        /// <param name="symbol">Имя символа.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Подготовленный ответ.</returns>
        public Task<FindReferencesResponseDto> FindAsync(string solutionPath, string symbol, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Возвращает заранее подготовленный ответ поиска реализаций без обращения к Roslyn.
    /// </summary>
    private sealed class StubFindImplementationsService(FindImplementationsResponseDto response) : IFindImplementationsService
    {
        /// <summary>
        /// Возвращает подготовленный ответ поиска реализаций.
        /// </summary>
        /// <param name="solutionPath">Путь к решению.</param>
        /// <param name="symbol">Имя символа.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Подготовленный ответ.</returns>
        public Task<FindImplementationsResponseDto> FindAsync(
            string solutionPath,
            string symbol,
            bool includeTests = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Возвращает заранее подготовленный ответ анализа влияния без обращения к Roslyn.
    /// </summary>
    private sealed class StubAnalyzeImpactService(AnalyzeImpactResponseDto response) : IAnalyzeImpactService
    {
        /// <summary>
        /// Возвращает подготовленный ответ анализа влияния.
        /// </summary>
        /// <param name="solutionPath">Путь к решению.</param>
        /// <param name="symbol">Имя символа.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Подготовленный ответ.</returns>
        public Task<AnalyzeImpactResponseDto> AnalyzeAsync(
            string solutionPath,
            string symbol,
            bool includeTests = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Выбрасывает заранее подготовленное исключение для проверки обработки ошибок в CLI.
    /// </summary>
    private sealed class ThrowingSolutionSummaryLoader(Exception exception) : ISolutionSummaryLoader
    {
        /// <summary>
        /// Завершает загрузку исключением без обращения к файловой системе.
        /// </summary>
        /// <param name="solutionPath">Путь к решению.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, завершающаяся исключением.</returns>
        public Task<SolutionSummaryDto> LoadAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            return Task.FromException<SolutionSummaryDto>(exception);
        }
    }

    /// <summary>
    /// Выбрасывает подготовленное исключение для проверки обработки ошибок команды поиска ссылок.
    /// </summary>
    private sealed class ThrowingFindReferencesService(Exception exception) : IFindReferencesService
    {
        /// <summary>
        /// Завершает поиск ссылок исключением без обращения к Roslyn.
        /// </summary>
        /// <param name="solutionPath">Путь к решению.</param>
        /// <param name="symbol">Имя символа.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, завершающаяся исключением.</returns>
        public Task<FindReferencesResponseDto> FindAsync(string solutionPath, string symbol, CancellationToken cancellationToken = default)
        {
            return Task.FromException<FindReferencesResponseDto>(exception);
        }
    }

    /// <summary>
    /// Выбрасывает подготовленное исключение для проверки обработки ошибок команды поиска реализаций.
    /// </summary>
    private sealed class ThrowingFindImplementationsService(Exception exception) : IFindImplementationsService
    {
        /// <summary>
        /// Завершает поиск реализаций исключением без обращения к Roslyn.
        /// </summary>
        /// <param name="solutionPath">Путь к решению.</param>
        /// <param name="symbol">Имя символа.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, завершающаяся исключением.</returns>
        public Task<FindImplementationsResponseDto> FindAsync(
            string solutionPath,
            string symbol,
            bool includeTests = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<FindImplementationsResponseDto>(exception);
        }
    }

    /// <summary>
    /// Выбрасывает подготовленное исключение для проверки обработки ошибок команды анализа влияния.
    /// </summary>
    private sealed class ThrowingAnalyzeImpactService(Exception exception) : IAnalyzeImpactService
    {
        /// <summary>
        /// Завершает анализ влияния исключением без обращения к Roslyn.
        /// </summary>
        /// <param name="solutionPath">Путь к решению.</param>
        /// <param name="symbol">Имя символа.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <returns>Задача, завершающаяся исключением.</returns>
        public Task<AnalyzeImpactResponseDto> AnalyzeAsync(
            string solutionPath,
            string symbol,
            bool includeTests = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<AnalyzeImpactResponseDto>(exception);
        }
    }
}
