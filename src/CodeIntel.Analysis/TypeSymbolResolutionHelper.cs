using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Contracts;
using Microsoft.CodeAnalysis;

namespace CodeIntel.Analysis;

internal static class TypeSymbolResolutionHelper
{
    private const string GlobalQualifier = "global::";

    internal static TypeSymbolQuery ParseQuery(string query)
    {
        var normalizedQuery = NormalizeFullyQualifiedName(query);
        var isQualified = query.StartsWith(GlobalQualifier, StringComparison.OrdinalIgnoreCase) || query.Contains('.', StringComparison.Ordinal);
        return new TypeSymbolQuery(query, normalizedQuery, isQualified);
    }

    internal static async Task<IReadOnlyList<TypeSymbolCandidate>> FindCandidatesAsync(
        Solution solution,
        TypeSymbolQuery query,
        IReadOnlyDictionary<string, string> projectNamesByFilePath,
        CancellationToken cancellationToken,
        int? maxResults = null)
    {
        var sourceMatches = new List<TypeSymbolCandidate>();
        var metadataMatches = new List<TypeSymbolCandidate>();
        var sourceSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var metadataSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects.OrderBy(p => p.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
            {
                continue;
            }

            CollectMatchingTypes(
                compilation.GlobalNamespace,
                solution,
                project,
                query,
                sourceMatches,
                metadataMatches,
                sourceSeen,
                metadataSeen,
                projectNamesByFilePath,
                cancellationToken,
                maxResults);

            if (maxResults is not null &&
                (sourceMatches.Count >= maxResults.Value || (sourceMatches.Count == 0 && metadataMatches.Count >= maxResults.Value)))
            {
                break;
            }
        }

        var matches = sourceMatches.Count > 0 ? sourceMatches : metadataMatches;

        return matches
            .OrderBy(item => item.Project, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FullyQualifiedName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Line)
            .ThenBy(item => item.Column)
            .ToArray();
    }

    internal static string BuildAmbiguousSymbolMessage(string symbol, IReadOnlyList<TypeSymbolCandidate> candidates)
    {
        var locations = candidates
            .Select(candidate => $"{candidate.FullyQualifiedName} ({candidate.Project}: {candidate.FilePath}:{candidate.Line}:{candidate.Column})");

        return $"Multiple matching type declarations were found for symbol '{symbol}': {string.Join("; ", locations)}";
    }

    internal static string NormalizeFullyQualifiedName(string fullyQualifiedName)
    {
        return fullyQualifiedName.StartsWith(GlobalQualifier, StringComparison.OrdinalIgnoreCase)
            ? fullyQualifiedName[GlobalQualifier.Length..]
            : fullyQualifiedName;
    }

    private static void CollectMatchingTypes(
        INamespaceSymbol @namespace,
        Solution solution,
        Project project,
        TypeSymbolQuery query,
        List<TypeSymbolCandidate> sourceMatches,
        List<TypeSymbolCandidate> metadataMatches,
        HashSet<string> sourceSeen,
        HashSet<string> metadataSeen,
        IReadOnlyDictionary<string, string> projectNamesByFilePath,
        CancellationToken cancellationToken,
        int? maxResults)
    {
        foreach (var child in @namespace.GetNamespaceMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectMatchingTypes(child, solution, project, query, sourceMatches, metadataMatches, sourceSeen, metadataSeen, projectNamesByFilePath, cancellationToken, maxResults);
            if (maxResults is not null &&
                (sourceMatches.Count >= maxResults.Value || (sourceMatches.Count == 0 && metadataMatches.Count >= maxResults.Value)))
            {
                return;
            }
        }

        foreach (var type in @namespace.GetTypeMembers())
        {
            CollectMatchingTypes(type, solution, project, query, sourceMatches, metadataMatches, sourceSeen, metadataSeen, projectNamesByFilePath, cancellationToken, maxResults);
            if (maxResults is not null &&
                (sourceMatches.Count >= maxResults.Value || (sourceMatches.Count == 0 && metadataMatches.Count >= maxResults.Value)))
            {
                return;
            }
        }
    }

    private static void CollectMatchingTypes(
        INamedTypeSymbol type,
        Solution solution,
        Project project,
        TypeSymbolQuery query,
        List<TypeSymbolCandidate> sourceMatches,
        List<TypeSymbolCandidate> metadataMatches,
        HashSet<string> sourceSeen,
        HashSet<string> metadataSeen,
        IReadOnlyDictionary<string, string> projectNamesByFilePath,
        CancellationToken cancellationToken,
        int? maxResults)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (MatchesQuery(type, query) &&
            AnalysisWorkspaceHelpers.TryMapSymbolKind(type.TypeKind, out var kind))
        {
            var isSource = AnalysisWorkspaceHelpers.TryGetPrimaryLocation(type, out var filePath, out var line, out var column);
            if (!isSource)
            {
                AnalysisWorkspaceHelpers.GetPreferredLocation(type, out filePath, out line, out column);
            }

            var key = AnalysisWorkspaceHelpers.BuildSymbolKey(type, filePath, line, column);
            var seen = isSource ? sourceSeen : metadataSeen;
            if (seen.Add(key))
            {
                var matches = isSource ? sourceMatches : metadataMatches;
                matches.Add(
                    new TypeSymbolCandidate(
                        type,
                        kind,
                        AnalysisWorkspaceHelpers.ResolveDeclaringProjectName(solution, project, filePath, projectNamesByFilePath),
                        filePath,
                        line,
                        column));
            }
        }

        if (maxResults is not null &&
            (sourceMatches.Count >= maxResults.Value || (sourceMatches.Count == 0 && metadataMatches.Count >= maxResults.Value)))
        {
            return;
        }

        foreach (var nested in type.GetTypeMembers())
        {
            CollectMatchingTypes(nested, solution, project, query, sourceMatches, metadataMatches, sourceSeen, metadataSeen, projectNamesByFilePath, cancellationToken, maxResults);
            if (maxResults is not null &&
                (sourceMatches.Count >= maxResults.Value || (sourceMatches.Count == 0 && metadataMatches.Count >= maxResults.Value)))
            {
                return;
            }
        }
    }

    private static bool MatchesQuery(INamedTypeSymbol type, TypeSymbolQuery query)
    {
        return query.IsQualified
            ? string.Equals(NormalizeFullyQualifiedName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)), query.NormalizedText, StringComparison.OrdinalIgnoreCase)
            : string.Equals(type.Name, query.RawText, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record TypeSymbolQuery(string RawText, string NormalizedText, bool IsQualified);

internal sealed record TypeSymbolCandidate(
    INamedTypeSymbol Symbol,
    SymbolKindDto Kind,
    string Project,
    string FilePath,
    int Line,
    int Column)
{
    public string FullyQualifiedName { get; } = Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
}
