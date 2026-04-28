using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Contracts;
using CodeIntel.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CodeIntel.Analysis;

/// <summary>
/// Реализует поиск реализаций интерфейсов и абстрактных классов внутри решения.
/// </summary>
public sealed class FindImplementationsService : IFindImplementationsService
{
    /// <summary>
    /// Инициализирует сервис поиска реализаций.
    /// </summary>
    public FindImplementationsService()
    {
    }

    /// <inheritdoc />
    public async Task<FindImplementationsResponseDto> FindAsync(
        string solutionPath,
        string symbol,
        bool includeTests = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var fullSolutionPath = Path.GetFullPath(solutionPath);
        using var workspace = AnalysisWorkspaceHelpers.CreateWorkspace(fullSolutionPath);
        var solution = await workspace.OpenSolutionAsync(fullSolutionPath, cancellationToken: cancellationToken);
        var projectNamesByFilePath = AnalysisWorkspaceHelpers.BuildProjectNamesByFilePath(solution);
        var query = TypeSymbolResolutionHelper.ParseQuery(symbol);
        var candidates = await TypeSymbolResolutionHelper.FindCandidatesAsync(solution, query, projectNamesByFilePath, cancellationToken);

        if (candidates.Count == 0)
        {
            return new FindImplementationsResponseDto
            {
                SolutionPath = fullSolutionPath,
                Symbol = symbol,
                Declaration = null,
                Implementations = Array.Empty<FindImplementationsResultDto>(),
                ImplementationCount = 0
            };
        }

        if (candidates.Count > 1)
        {
            throw new InvalidOperationException(TypeSymbolResolutionHelper.BuildAmbiguousSymbolMessage(symbol, candidates));
        }

        var candidate = candidates[0];
        var declaration = new FindImplementationsDeclarationDto
        {
            Symbol = candidate.Symbol.Name,
            FullyQualifiedName = candidate.FullyQualifiedName,
            Kind = candidate.Kind,
            Project = candidate.Project,
            FilePath = candidate.FilePath,
            Line = candidate.Line,
            Column = candidate.Column
        };

        if (!IsSupportedDeclarationKind(candidate.Symbol))
        {
            return new FindImplementationsResponseDto
            {
                SolutionPath = fullSolutionPath,
                Symbol = symbol,
                Declaration = declaration,
                Implementations = Array.Empty<FindImplementationsResultDto>(),
                ImplementationCount = 0
            };
        }

        var implementations = await FindImplementationLocationsAsync(
            candidate.Symbol,
            solution,
            projectNamesByFilePath,
            includeTests,
            cancellationToken);

        return new FindImplementationsResponseDto
        {
            SolutionPath = fullSolutionPath,
            Symbol = symbol,
            Declaration = declaration,
            Implementations = implementations,
            ImplementationCount = implementations.Count
        };
    }

    private static bool IsSupportedDeclarationKind(INamedTypeSymbol symbol)
    {
        return symbol.TypeKind == TypeKind.Interface || (symbol.TypeKind == TypeKind.Class && symbol.IsAbstract);
    }

    private static async Task<IReadOnlyList<FindImplementationsResultDto>> FindImplementationLocationsAsync(
        INamedTypeSymbol declaration,
        Solution solution,
        IReadOnlyDictionary<string, string> projectNamesByFilePath,
        bool includeTests,
        CancellationToken cancellationToken)
    {
        var results = new List<FindImplementationsResultDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var testProjectPaths = includeTests
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : BuildTestProjectPaths(solution);
        var implementations = declaration.TypeKind == TypeKind.Interface
            ? await SymbolFinder.FindImplementationsAsync(declaration, solution, cancellationToken: cancellationToken)
            : await FindDerivedTypesAsync(declaration, solution, cancellationToken);

        foreach (var implementation in implementations.OfType<INamedTypeSymbol>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!AnalysisWorkspaceHelpers.TryMapSymbolKind(implementation.TypeKind, out var kind) ||
                !AnalysisWorkspaceHelpers.TryGetPrimaryLocation(implementation, out var filePath, out var line, out var column))
            {
                continue;
            }

            var key = AnalysisWorkspaceHelpers.BuildSymbolKey(implementation, filePath, line, column);
            if (!seen.Add(key))
            {
                continue;
            }
            var documentId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
            var project = documentId is not null ? solution.GetProject(documentId.ProjectId) : null;
            if (!includeTests && project?.FilePath is not null && testProjectPaths.Contains(project.FilePath))
            {
                continue;
            }

            results.Add(new FindImplementationsResultDto
            {
                Symbol = implementation.Name,
                FullyQualifiedName = implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Kind = kind,
                Project = project is not null
                    ? AnalysisWorkspaceHelpers.ResolveDeclaringProjectName(solution, project, filePath, projectNamesByFilePath)
                    : projectNamesByFilePath.TryGetValue(filePath, out var projectName)
                        ? projectName
                        : "Unknown",
                FilePath = filePath,
                Line = line,
                Column = column
            });
        }

        return results
            .OrderBy(item => item.Project, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FullyQualifiedName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Line)
            .ThenBy(item => item.Column)
            .ToArray();
    }

    private static HashSet<string> BuildTestProjectPaths(Solution solution)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects)
        {
            if (string.IsNullOrWhiteSpace(project.FilePath) || string.IsNullOrWhiteSpace(project.Name))
            {
                continue;
            }

            if (TestProjectClassifier.IsTestProject(project.Name, project.FilePath))
            {
                result.Add(project.FilePath);
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<INamedTypeSymbol>> FindDerivedTypesAsync(
        INamedTypeSymbol declaration,
        Solution solution,
        CancellationToken cancellationToken)
    {
        var results = new List<INamedTypeSymbol>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects.OrderBy(p => p.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            CollectDerivedTypes(compilation.GlobalNamespace, declaration, results, seen, cancellationToken);
        }

        return results;
    }

    private static void CollectDerivedTypes(
        INamespaceSymbol @namespace,
        INamedTypeSymbol declaration,
        List<INamedTypeSymbol> results,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        foreach (var child in @namespace.GetNamespaceMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectDerivedTypes(child, declaration, results, seen, cancellationToken);
        }

        foreach (var type in @namespace.GetTypeMembers())
        {
            CollectDerivedTypes(type, declaration, results, seen, cancellationToken);
        }
    }

    private static void CollectDerivedTypes(
        INamedTypeSymbol type,
        INamedTypeSymbol declaration,
        List<INamedTypeSymbol> results,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (DerivesFrom(type, declaration) && seen.Add(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
        {
            results.Add(type);
        }

        foreach (var nested in type.GetTypeMembers())
        {
            CollectDerivedTypes(nested, declaration, results, seen, cancellationToken);
        }
    }

    private static bool DerivesFrom(INamedTypeSymbol candidate, INamedTypeSymbol declaration)
    {
        for (var current = candidate.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, declaration))
            {
                return true;
            }
        }

        return false;
    }
}
