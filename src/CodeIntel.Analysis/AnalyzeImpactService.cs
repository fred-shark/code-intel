using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Contracts;
using CodeIntel.Loader;

namespace CodeIntel.Analysis;

/// <summary>
/// Агрегирует ссылки, реализации и затронутые проекты для базового анализа влияния изменений.
/// </summary>
public sealed class AnalyzeImpactService(
    IFindReferencesService findReferencesService,
    IFindImplementationsService findImplementationsService) : IAnalyzeImpactService
{
    private const string LowRisk = "Low";
    private const string MediumRisk = "Medium";
    private const string HighRisk = "High";
    private const string UnknownRisk = "Unknown";
    private const int HighReferenceCountThreshold = 10;

    /// <inheritdoc />
    public async Task<AnalyzeImpactResponseDto> AnalyzeAsync(
        string solutionPath,
        string symbol,
        bool includeTests = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var referencesResponse = await findReferencesService.FindAsync(solutionPath, symbol, cancellationToken);
        var implementationsResponse = await findImplementationsService.FindAsync(
            solutionPath,
            symbol,
            includeTests,
            cancellationToken);
        var filteredReferences = includeTests
            ? referencesResponse.References
            : FilterTestProjectReferences(referencesResponse.References);

        var declaration = referencesResponse.Declaration is not null
            ? MapDeclaration(referencesResponse.Declaration)
            : implementationsResponse.Declaration is not null
                ? MapDeclaration(implementationsResponse.Declaration)
                : null;

        if (declaration is null)
        {
            return new AnalyzeImpactResponseDto
            {
                SolutionPath = Path.GetFullPath(solutionPath),
                Symbol = symbol,
                Declaration = null,
                ReferenceCount = 0,
                ImplementationCount = 0,
                AffectedProjects = Array.Empty<string>(),
                RiskSummary = UnknownRisk
            };
        }

        var affectedProjects = BuildAffectedProjects(
            declaration.Project,
            filteredReferences.Select(static item => item.Project),
            implementationsResponse.Implementations.Select(static item => item.Project));
        var filteredReferenceCount = filteredReferences.Count;

        return new AnalyzeImpactResponseDto
        {
            SolutionPath = referencesResponse.SolutionPath,
            Symbol = symbol,
            Declaration = declaration,
            ReferenceCount = filteredReferenceCount,
            ImplementationCount = implementationsResponse.ImplementationCount,
            AffectedProjects = affectedProjects,
            RiskSummary = BuildRiskSummary(
                filteredReferenceCount,
                implementationsResponse.ImplementationCount,
                affectedProjects.Count)
        };
    }

    private static IReadOnlyList<FindReferencesResultDto> FilterTestProjectReferences(
        IReadOnlyList<FindReferencesResultDto> references)
    {
        return references
            .Where(static item => !string.IsNullOrWhiteSpace(item.Project) && !TestProjectClassifier.IsTestProjectName(item.Project))
            .ToArray();
    }

    private static AnalyzeImpactDeclarationDto MapDeclaration(FindReferencesDeclarationDto declaration)
    {
        return new AnalyzeImpactDeclarationDto
        {
            Symbol = declaration.Symbol,
            FullyQualifiedName = declaration.FullyQualifiedName,
            Kind = declaration.Kind,
            Project = declaration.Project,
            FilePath = declaration.FilePath,
            Line = declaration.Line,
            Column = declaration.Column
        };
    }

    private static AnalyzeImpactDeclarationDto MapDeclaration(FindImplementationsDeclarationDto declaration)
    {
        return new AnalyzeImpactDeclarationDto
        {
            Symbol = declaration.Symbol,
            FullyQualifiedName = declaration.FullyQualifiedName,
            Kind = declaration.Kind,
            Project = declaration.Project,
            FilePath = declaration.FilePath,
            Line = declaration.Line,
            Column = declaration.Column
        };
    }

    private static IReadOnlyList<string> BuildAffectedProjects(
        string declarationProject,
        IEnumerable<string> referenceProjects,
        IEnumerable<string> implementationProjects)
    {
        var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(declarationProject))
        {
            result.Add(declarationProject);
        }

        AddProjects(result, referenceProjects);
        AddProjects(result, implementationProjects);

        return result.ToArray();
    }

    private static void AddProjects(ISet<string> target, IEnumerable<string> projects)
    {
        foreach (var project in projects)
        {
            if (!string.IsNullOrWhiteSpace(project))
            {
                target.Add(project);
            }
        }
    }

    private static string BuildRiskSummary(int referenceCount, int implementationCount, int affectedProjectCount)
    {
        if (affectedProjectCount == 0)
        {
            return UnknownRisk;
        }

        if (referenceCount == 0 && implementationCount == 0 && affectedProjectCount == 1)
        {
            return LowRisk;
        }

        if (affectedProjectCount >= 3 || referenceCount >= HighReferenceCountThreshold)
        {
            return HighRisk;
        }

        return MediumRisk;
    }
}
