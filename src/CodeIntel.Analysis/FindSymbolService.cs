using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeIntel.Analysis;

/// <summary>
/// Реализует поиск символов по имени внутри решения.
/// </summary>
public sealed class FindSymbolService : IFindSymbolService
{
    private const int MaxResults = 20;

    /// <summary>
    /// Инициализирует сервис и подготавливает регистрацию MSBuild для Roslyn.
    /// </summary>
    public FindSymbolService()
    {
        AnalysisWorkspaceHelpers.RegisterMsBuild();
    }

    public async Task<FindSymbolResponseDto> FindAsync(string solutionPath, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var fullSolutionPath = Path.GetFullPath(solutionPath);
        using var workspace = MSBuildWorkspace.Create();

        var solution = await workspace.OpenSolutionAsync(fullSolutionPath, cancellationToken: cancellationToken);
        var projectNamesByFilePath = AnalysisWorkspaceHelpers.BuildProjectNamesByFilePath(solution);
        var query = TypeSymbolResolutionHelper.ParseQuery(name);
        var results = (await TypeSymbolResolutionHelper.FindCandidatesAsync(
                solution,
                query,
                projectNamesByFilePath,
                cancellationToken,
                MaxResults))
            .Select(static candidate => new FindSymbolResultDto
            {
                Name = candidate.Symbol.Name,
                FullyQualifiedName = candidate.FullyQualifiedName,
                Kind = candidate.Kind,
                Project = candidate.Project,
                FilePath = candidate.FilePath,
                Line = candidate.Line,
                Column = candidate.Column
            })
            .OrderBy(result => result.Project, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Line)
            .ThenBy(result => result.Column)
            .Take(MaxResults)
            .ToArray();

        return new FindSymbolResponseDto
        {
            SolutionPath = fullSolutionPath,
            Name = name,
            Results = results
        };
    }
}
