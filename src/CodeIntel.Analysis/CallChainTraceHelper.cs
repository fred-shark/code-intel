using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Contracts;
using CodeIntel.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CodeIntel.Analysis;

internal static class CallChainTraceHelper
{
    public static async Task<IReadOnlyList<CallChainNodeDto>> FindCallersRecursiveAsync(
        Solution solution,
        IMethodSymbol targetMethod,
        IReadOnlyDictionary<string, string> projectNamesByFilePath,
        HashSet<string> testProjectPaths,
        HashSet<string> visitedMethodFqns,
        int depth,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        if (depth >= maxDepth)
        {
            return Array.Empty<CallChainNodeDto>();
        }

        var callerInfos = await SymbolFinder.FindCallersAsync(targetMethod, solution, cancellationToken);
        var nodes = new List<CallChainNodeDto>();

        foreach (var callerInfo in callerInfos)
        {
            if (callerInfo.CallingSymbol is not IMethodSymbol callingMethod)
            {
                continue;
            }

            var callingMethodFqn = callingMethod.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            foreach (var callLocation in callerInfo.Locations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!callLocation.IsInSource || callLocation.SourceTree is null)
                {
                    continue;
                }

                var callFilePath = callLocation.SourceTree.FilePath;
                if (string.IsNullOrWhiteSpace(callFilePath))
                {
                    continue;
                }

                var documentId = solution.GetDocumentIdsWithFilePath(callFilePath).FirstOrDefault();
                var callProject = documentId is not null ? solution.GetProject(documentId.ProjectId) : null;

                if (callProject?.FilePath is not null && testProjectPaths.Contains(callProject.FilePath))
                {
                    continue;
                }

                var callPosition = callLocation.GetLineSpan().StartLinePosition;
                var callLine = callPosition.Line + 1;
                var callColumn = callPosition.Character + 1;

                if (!AnalysisWorkspaceHelpers.TryGetPrimaryLocation(callingMethod, out var callerFilePath, out var callerLine, out var callerColumn))
                {
                    callerFilePath = callFilePath;
                    callerLine = callLine;
                    callerColumn = callColumn;
                }

                var projectName = callProject is not null
                    ? AnalysisWorkspaceHelpers.ResolveDeclaringProjectName(solution, callProject, callFilePath, projectNamesByFilePath)
                    : projectNamesByFilePath.TryGetValue(callFilePath, out var pn) ? pn : "Unknown";

                var (callCondition, branch) = await ExtractCallConditionAsync(callLocation, cancellationToken);

                var callSite = new CallSiteDto
                {
                    FilePath = callFilePath,
                    Line = callLine,
                    Column = callColumn,
                    CallCondition = callCondition,
                    Branch = branch
                };

                var alreadyVisited = !visitedMethodFqns.Add(callingMethodFqn);

                IReadOnlyList<CallChainNodeDto> calledBy;
                if (alreadyVisited)
                {
                    calledBy = Array.Empty<CallChainNodeDto>();
                }
                else
                {
                    calledBy = await FindCallersRecursiveAsync(
                        solution,
                        callingMethod,
                        projectNamesByFilePath,
                        testProjectPaths,
                        visitedMethodFqns,
                        depth + 1,
                        maxDepth,
                        cancellationToken);
                }

                nodes.Add(new CallChainNodeDto
                {
                    ContainingType = callingMethod.ContainingType?.Name ?? string.Empty,
                    FullyQualifiedContainingType = callingMethod.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    Method = callingMethod.Name,
                    Project = projectName,
                    FilePath = callerFilePath,
                    Line = callerLine,
                    Column = callerColumn,
                    CallSite = callSite,
                    IsEntryPoint = !alreadyVisited && calledBy.Count == 0,
                    CalledBy = calledBy
                });
            }
        }

        return nodes;
    }

    public static async Task<(string? callCondition, string? branch)> ExtractCallConditionAsync(
        Location callLocation,
        CancellationToken cancellationToken)
    {
        var syntaxTree = callLocation.SourceTree;
        if (syntaxTree is null)
        {
            return (null, null);
        }

        var root = await syntaxTree.GetRootAsync(cancellationToken);
        var node = root.FindNode(callLocation.SourceSpan);
        var ifStatement = node.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();

        if (ifStatement is null)
        {
            return (null, null);
        }

        var conditionText = ifStatement.Condition.ToString();

        string branch;
        if (ifStatement.Statement.Span.Contains(callLocation.SourceSpan))
        {
            branch = "then";
        }
        else if (ifStatement.Else?.Statement.Span.Contains(callLocation.SourceSpan) == true)
        {
            branch = "else";
        }
        else
        {
            return (null, null);
        }

        return (conditionText, branch);
    }

    public static HashSet<string> BuildTestProjectPaths(Solution solution)
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

    public static int CountEntryPoints(IReadOnlyList<CallChainNodeDto> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (node.IsEntryPoint)
            {
                count++;
            }

            count += CountEntryPoints(node.CalledBy);
        }

        return count;
    }
}
