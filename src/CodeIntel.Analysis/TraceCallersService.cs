using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Contracts;
using CodeIntel.Loader;
using Microsoft.CodeAnalysis;

namespace CodeIntel.Analysis;

/// <summary>
/// Реализует трассировку цепочки вызовов метода до точек входа через Roslyn.
/// </summary>
public sealed class TraceCallersService : ITraceCallersService
{
    /// <summary>
    /// Инициализирует сервис трассировки вызовов.
    /// </summary>
    public TraceCallersService()
    {
    }

    /// <inheritdoc />
    public async Task<TraceCallersResponseDto> FindAsync(
        string solutionPath,
        string symbol,
        string method,
        int maxDepth = 15,
        bool includeTests = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        var fullSolutionPath = Path.GetFullPath(solutionPath);
        using var workspace = AnalysisWorkspaceHelpers.CreateWorkspace(fullSolutionPath);
        var solution = await workspace.OpenSolutionAsync(fullSolutionPath, cancellationToken: cancellationToken);
        var projectNamesByFilePath = AnalysisWorkspaceHelpers.BuildProjectNamesByFilePath(solution);
        var query = TypeSymbolResolutionHelper.ParseQuery(symbol);
        var candidates = await TypeSymbolResolutionHelper.FindCandidatesAsync(solution, query, projectNamesByFilePath, cancellationToken);

        if (candidates.Count == 0)
        {
            return new TraceCallersResponseDto
            {
                SolutionPath = fullSolutionPath,
                Symbol = symbol,
                Method = method,
                Declaration = null,
                CallChains = Array.Empty<CallChainNodeDto>(),
                EntryPointCount = 0
            };
        }

        if (candidates.Count > 1)
        {
            throw new InvalidOperationException(TypeSymbolResolutionHelper.BuildAmbiguousSymbolMessage(symbol, candidates));
        }

        var candidate = candidates[0];
        var typeSymbol = candidate.Symbol;

        var methodSymbols = typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => string.Equals(m.Name, method, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var declaration = BuildDeclaration(methodSymbols.Length > 0 ? methodSymbols[0] : null, candidate);

        if (methodSymbols.Length == 0)
        {
            return new TraceCallersResponseDto
            {
                SolutionPath = fullSolutionPath,
                Symbol = symbol,
                Method = method,
                Declaration = declaration,
                CallChains = Array.Empty<CallChainNodeDto>(),
                EntryPointCount = 0
            };
        }

        var testProjectPaths = includeTests
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : CallChainTraceHelper.BuildTestProjectPaths(solution);

        var allCallChains = new List<CallChainNodeDto>();

        foreach (var methodSymbol in methodSymbols)
        {
            var visitedMethodFqns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var chains = await CallChainTraceHelper.FindCallersRecursiveAsync(
                solution,
                methodSymbol,
                projectNamesByFilePath,
                testProjectPaths,
                visitedMethodFqns,
                depth: 0,
                maxDepth: maxDepth,
                cancellationToken: cancellationToken);
            allCallChains.AddRange(chains);
        }

        return new TraceCallersResponseDto
        {
            SolutionPath = fullSolutionPath,
            Symbol = symbol,
            Method = method,
            Declaration = declaration,
            CallChains = allCallChains,
            EntryPointCount = CallChainTraceHelper.CountEntryPoints(allCallChains)
        };
    }

    private static TraceCallersDeclarationDto BuildDeclaration(IMethodSymbol? methodSymbol, TypeSymbolCandidate candidate)
    {
        if (methodSymbol is not null)
        {
            AnalysisWorkspaceHelpers.GetPreferredLocation(methodSymbol, out var filePath, out var line, out var column);

            return new TraceCallersDeclarationDto
            {
                Symbol = candidate.Symbol.Name,
                Method = methodSymbol.Name,
                FullyQualifiedTypeName = candidate.FullyQualifiedName,
                Kind = candidate.Kind,
                Project = candidate.Project,
                FilePath = filePath,
                Line = line,
                Column = column
            };
        }

        return new TraceCallersDeclarationDto
        {
            Symbol = candidate.Symbol.Name,
            Method = methodSymbol?.Name ?? string.Empty,
            FullyQualifiedTypeName = candidate.FullyQualifiedName,
            Kind = candidate.Kind,
            Project = candidate.Project,
            FilePath = candidate.FilePath,
            Line = candidate.Line,
            Column = candidate.Column
        };
    }
}
