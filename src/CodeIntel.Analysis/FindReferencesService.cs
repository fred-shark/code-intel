using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CodeIntel.Analysis;

/// <summary>
/// Реализует поиск ссылок на типы внутри решения.
/// </summary>
public sealed class FindReferencesService : IFindReferencesService
{
    /// <summary>
    /// Инициализирует сервис поиска ссылок.
    /// </summary>
    public FindReferencesService()
    {
    }

    /// <inheritdoc />
    public async Task<FindReferencesResponseDto> FindAsync(
        string solutionPath,
        string symbol,
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
            return new FindReferencesResponseDto
            {
                SolutionPath = fullSolutionPath,
                Symbol = symbol,
                Declaration = null,
                References = Array.Empty<FindReferencesResultDto>(),
                ReferenceCount = 0
            };
        }

        if (candidates.Count > 1)
        {
            throw new InvalidOperationException(TypeSymbolResolutionHelper.BuildAmbiguousSymbolMessage(symbol, candidates));
        }

        var candidate = candidates[0];
        var declaration = new FindReferencesDeclarationDto
        {
            Symbol = candidate.Symbol.Name,
            FullyQualifiedName = candidate.FullyQualifiedName,
            Kind = candidate.Kind,
            Project = candidate.Project,
            FilePath = candidate.FilePath,
            Line = candidate.Line,
            Column = candidate.Column
        };

        var references = await FindReferenceLocationsAsync(
            candidate.Symbol,
            solution,
            candidate.FullyQualifiedName,
            projectNamesByFilePath,
            cancellationToken);

        return new FindReferencesResponseDto
        {
            SolutionPath = fullSolutionPath,
            Symbol = symbol,
            Declaration = declaration,
            References = references,
            ReferenceCount = references.Count
        };
    }

    private static async Task<IReadOnlyList<FindReferencesResultDto>> FindReferenceLocationsAsync(
        INamedTypeSymbol declaration,
        Solution solution,
        string fullyQualifiedName,
        IReadOnlyDictionary<string, string> projectNamesByFilePath,
        CancellationToken cancellationToken)
    {
        var results = new List<FindReferencesResultDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referencedSymbols = await SymbolFinder.FindReferencesAsync(declaration, solution, cancellationToken);

        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var location in referencedSymbol.Locations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!location.Location.IsInSource || location.Document is null)
                {
                    continue;
                }

                var sourceTree = location.Location.SourceTree;
                if (sourceTree is null)
                {
                    continue;
                }

                var filePath = sourceTree.FilePath;
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    continue;
                }

                var position = location.Location.GetLineSpan().StartLinePosition;
                var line = position.Line + 1;
                var column = position.Character + 1;
                var key = $"{filePath}|{line}|{column}";

                if (!seen.Add(key))
                {
                    continue;
                }

                results.Add(new FindReferencesResultDto
                {
                    Symbol = declaration.Name,
                    ReferencedSymbolFullyQualifiedName = fullyQualifiedName,
                    Project = AnalysisWorkspaceHelpers.ResolveDeclaringProjectName(
                        solution,
                        location.Document.Project,
                        filePath,
                        projectNamesByFilePath),
                    FilePath = filePath,
                    Line = line,
                    Column = column
                });
            }
        }

        return results
            .OrderBy(item => item.Project, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Line)
            .ThenBy(item => item.Column)
            .ToArray();
    }
}
