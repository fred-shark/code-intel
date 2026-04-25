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
/// Проверяет CLI-оболочку для команды find-registrations.
/// </summary>
public sealed class FindRegistrationsCliApplicationTests
{
    [Fact]
    public async Task RunAsync_WritesFindRegistrationsJson_ForValidArguments()
    {
        var app = CreateApplication(
            new FindRegistrationsResponseDto
            {
                SolutionPath = "/repo/sample.slnx",
                Symbol = "IService",
                Declaration = new FindRegistrationsDeclarationDto
                {
                    Symbol = "IService",
                    FullyQualifiedName = "global::Sample.IService",
                    Kind = SymbolKindDto.Interface,
                    Project = "App",
                    FilePath = "/repo/src/App/IService.cs",
                    Line = 3,
                    Column = 18
                },
                Registrations =
                [
                    new FindRegistrationsResultDto
                    {
                        ServiceSymbol = "global::Sample.IService",
                        ImplementationSymbol = "global::Sample.Service",
                        Lifetime = "Transient",
                        RegistrationFramework = RegistrationFrameworkDto.CastleWindsor,
                        Project = "App",
                        FilePath = "/repo/src/App/CompositionRoot.cs",
                        Line = 10,
                        Column = 9
                    }
                ],
                RegistrationCount = 1
            });
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-registrations", "--solution", "sample.slnx", "--symbol", "IService"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var payload = output.ToString();
        Assert.Contains("\"symbol\": \"IService\"", payload);
        Assert.Contains("\"registrations\": [", payload);
        Assert.Contains("\"registrationFramework\": \"CastleWindsor\"", payload);
        Assert.Contains("\"registrationCount\": 1", payload);
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForMissingSymbolOption_ForFindRegistrations()
    {
        var app = CreateApplication(CreateEmptyResponse());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-registrations", "--solution", "sample.slnx"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Missing required --symbol option.", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForAmbiguousFindRegistrationsSymbol()
    {
        var app = new CliApplication(
            new StubSolutionSummaryLoader(),
            new StubFindSymbolService(),
            new StubFindReferencesService(),
            new StubFindImplementationsService(),
            new StubAnalyzeImpactService(),
            new ThrowingFindRegistrationsService(new InvalidOperationException("Multiple matching type declarations were found for symbol 'Target'.")));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await app.RunAsync(
            ["find-registrations", "--solution", "sample.slnx", "--symbol", "Target"],
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Multiple matching type declarations were found", error.ToString());
    }

    private static CliApplication CreateApplication(FindRegistrationsResponseDto response)
    {
        return new CliApplication(
            new StubSolutionSummaryLoader(),
            new StubFindSymbolService(),
            new StubFindReferencesService(),
            new StubFindImplementationsService(),
            new StubAnalyzeImpactService(),
            new StubFindRegistrationsService(response));
    }

    private static FindRegistrationsResponseDto CreateEmptyResponse()
    {
        return new FindRegistrationsResponseDto
        {
            SolutionPath = string.Empty,
            Symbol = string.Empty,
            Declaration = null,
            Registrations = Array.Empty<FindRegistrationsResultDto>(),
            RegistrationCount = 0
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

    private sealed class StubFindRegistrationsService(FindRegistrationsResponseDto response) : IFindRegistrationsService
    {
        public Task<FindRegistrationsResponseDto> FindAsync(
            string solutionPath,
            string symbol,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingFindRegistrationsService(Exception exception) : IFindRegistrationsService
    {
        public Task<FindRegistrationsResponseDto> FindAsync(
            string solutionPath,
            string symbol,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<FindRegistrationsResponseDto>(exception);
        }
    }
}
