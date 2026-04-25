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
/// Проверяет CLI-оболочку для команды trace-property-callers.
/// </summary>
public sealed class TracePropertyCallersCliApplicationTests
{
    [Fact]
    public async Task RunAsync_WritesTracePropertyCallersJson_ForValidArguments()
    {
        var app = CreateApplication(
            new TracePropertyCallersResponseDto
            {
                SolutionPath = "/repo/sample.slnx",
                Symbol = "FindReferencesResponseDto",
                Property = "SolutionPath",
                Access = PropertyAccessKindDto.Set,
                Declaration = new TracePropertyCallersDeclarationDto
                {
                    Symbol = "FindReferencesResponseDto",
                    Property = "SolutionPath",
                    FullyQualifiedTypeName = "global::CodeIntel.Contracts.FindReferencesResponseDto",
                    Kind = SymbolKindDto.Class,
                    Project = "CodeIntel.Contracts",
                    FilePath = "/repo/src/CodeIntel.Contracts/FindReferencesResponseDto.cs",
                    Line = 11,
                    Column = 28,
                    HasGetter = true,
                    HasSetter = true
                },
                AccessChains =
                [
                    new PropertyAccessCallChainsDto
                    {
                        Access = PropertyAccessKindDto.Set,
                        EntryPointCount = 1,
                        CallChains =
                        [
                            new CallChainNodeDto
                            {
                                ContainingType = "FindReferencesService",
                                FullyQualifiedContainingType = "global::CodeIntel.Analysis.FindReferencesService",
                                Method = "FindAsync",
                                Project = "CodeIntel.Analysis",
                                FilePath = "/repo/src/CodeIntel.Analysis/FindReferencesService.cs",
                                Line = 28,
                                Column = 5,
                                CallSite = new CallSiteDto
                                {
                                    FilePath = "/repo/src/CodeIntel.Analysis/FindReferencesService.cs",
                                    Line = 81,
                                    Column = 13,
                                    CallCondition = null,
                                    Branch = null
                                },
                                IsEntryPoint = true,
                                CalledBy = Array.Empty<CallChainNodeDto>()
                            }
                        ]
                    }
                ]
            });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["trace-property-callers", "--solution", "sample.slnx", "--symbol", "FindReferencesResponseDto", "--property", "SolutionPath", "--access", "set"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var payload = output.ToString();
        Assert.Contains("\"property\": \"SolutionPath\"", payload);
        Assert.Contains("\"access\": \"Set\"", payload);
        Assert.Contains("\"accessChains\": [", payload);
        Assert.Contains("\"entryPointCount\": 1", payload);
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForMissingPropertyOption()
    {
        var app = CreateApplication(CreateEmptyResponse());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["trace-property-callers", "--solution", "sample.slnx", "--symbol", "FindReferencesResponseDto"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Missing required --property option.", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForInvalidAccessOption()
    {
        var app = CreateApplication(CreateEmptyResponse());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["trace-property-callers", "--solution", "sample.slnx", "--symbol", "FindReferencesResponseDto", "--property", "SolutionPath", "--access", "write"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("The --access option must be one of: get, set, both.", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForAmbiguousPropertyTraceSymbol()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(),
            new StubFindSymbolService(),
            new StubFindReferencesService(),
            new StubFindImplementationsService(),
            new StubAnalyzeImpactService(),
            new StubFindRegistrationsService(),
            new StubTraceCallersService(),
            new ThrowingTracePropertyCallersService(new InvalidOperationException("Multiple matching type declarations were found for symbol 'Target'.")));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["trace-property-callers", "--solution", "sample.slnx", "--symbol", "Target", "--property", "SolutionPath"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Multiple matching type declarations were found", error.ToString());
    }

    private static CliApplication CreateApplication(TracePropertyCallersResponseDto response)
    {
        return new CliApplication(
            new StubSolutionSummaryLoader(),
            new StubFindSymbolService(),
            new StubFindReferencesService(),
            new StubFindImplementationsService(),
            new StubAnalyzeImpactService(),
            new StubFindRegistrationsService(),
            new StubTraceCallersService(),
            new StubTracePropertyCallersService(response));
    }

    private static TracePropertyCallersResponseDto CreateEmptyResponse()
    {
        return new TracePropertyCallersResponseDto
        {
            SolutionPath = string.Empty,
            Symbol = string.Empty,
            Property = string.Empty,
            Access = PropertyAccessKindDto.Both,
            Declaration = null,
            AccessChains = Array.Empty<PropertyAccessCallChainsDto>()
        };
    }

    private sealed class StubSolutionSummaryLoader : ISolutionSummaryLoader
    {
        public Task<SolutionSummaryDto> LoadAsync(string solutionPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new SolutionSummaryDto
                {
                    SolutionPath = "/repo/sample.slnx",
                    Projects = Array.Empty<ProjectSummaryDto>()
                });
        }
    }

    private sealed class StubFindSymbolService : IFindSymbolService
    {
        public Task<FindSymbolResponseDto> FindAsync(string solutionPath, string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new FindSymbolResponseDto
                {
                    SolutionPath = string.Empty,
                    Name = string.Empty,
                    Results = Array.Empty<FindSymbolResultDto>()
                });
        }
    }

    private sealed class StubFindReferencesService : IFindReferencesService
    {
        public Task<FindReferencesResponseDto> FindAsync(string solutionPath, string symbol, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new FindReferencesResponseDto
                {
                    SolutionPath = string.Empty,
                    Symbol = string.Empty,
                    Declaration = null,
                    References = Array.Empty<FindReferencesResultDto>(),
                    ReferenceCount = 0
                });
        }
    }

    private sealed class StubFindImplementationsService : IFindImplementationsService
    {
        public Task<FindImplementationsResponseDto> FindAsync(
            string solutionPath,
            string symbol,
            bool includeTests = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new FindImplementationsResponseDto
                {
                    SolutionPath = string.Empty,
                    Symbol = string.Empty,
                    Declaration = null,
                    Implementations = Array.Empty<FindImplementationsResultDto>(),
                    ImplementationCount = 0
                });
        }
    }

    private sealed class StubAnalyzeImpactService : IAnalyzeImpactService
    {
        public Task<AnalyzeImpactResponseDto> AnalyzeAsync(
            string solutionPath,
            string symbol,
            bool includeTests = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new AnalyzeImpactResponseDto
                {
                    SolutionPath = string.Empty,
                    Symbol = string.Empty,
                    Declaration = null,
                    ReferenceCount = 0,
                    ImplementationCount = 0,
                    AffectedProjects = Array.Empty<string>(),
                    RiskSummary = "Unknown"
                });
        }
    }

    private sealed class StubFindRegistrationsService : IFindRegistrationsService
    {
        public Task<FindRegistrationsResponseDto> FindAsync(
            string solutionPath,
            string symbol,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new FindRegistrationsResponseDto
                {
                    SolutionPath = string.Empty,
                    Symbol = string.Empty,
                    Declaration = null,
                    Registrations = Array.Empty<FindRegistrationsResultDto>(),
                    RegistrationCount = 0
                });
        }
    }

    private sealed class StubTraceCallersService : ITraceCallersService
    {
        public Task<TraceCallersResponseDto> FindAsync(
            string solutionPath,
            string symbol,
            string method,
            int maxDepth = 15,
            bool includeTests = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new TraceCallersResponseDto
                {
                    SolutionPath = string.Empty,
                    Symbol = string.Empty,
                    Method = string.Empty,
                    Declaration = null,
                    CallChains = Array.Empty<CallChainNodeDto>(),
                    EntryPointCount = 0
                });
        }
    }

    private sealed class StubTracePropertyCallersService(TracePropertyCallersResponseDto response) : ITracePropertyCallersService
    {
        public Task<TracePropertyCallersResponseDto> FindAsync(
            string solutionPath,
            string symbol,
            string property,
            PropertyAccessKindDto access = PropertyAccessKindDto.Both,
            int maxDepth = 15,
            bool includeTests = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingTracePropertyCallersService(Exception exception) : ITracePropertyCallersService
    {
        public Task<TracePropertyCallersResponseDto> FindAsync(
            string solutionPath,
            string symbol,
            string property,
            PropertyAccessKindDto access = PropertyAccessKindDto.Both,
            int maxDepth = 15,
            bool includeTests = false,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<TracePropertyCallersResponseDto>(exception);
        }
    }
}
