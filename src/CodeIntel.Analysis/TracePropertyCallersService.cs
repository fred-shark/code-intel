using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Contracts;
using CodeIntel.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeIntel.Analysis;

/// <summary>
/// Реализует трассировку цепочек вызовов для чтений и записей свойства через Roslyn.
/// </summary>
public sealed class TracePropertyCallersService : ITracePropertyCallersService
{
    /// <summary>
    /// Инициализирует сервис и подготавливает регистрацию MSBuild для Roslyn.
    /// </summary>
    public TracePropertyCallersService()
    {
        AnalysisWorkspaceHelpers.RegisterMsBuild();
    }

    /// <inheritdoc />
    public async Task<TracePropertyCallersResponseDto> FindAsync(
        string solutionPath,
        string symbol,
        string property,
        PropertyAccessKindDto access = PropertyAccessKindDto.Both,
        int maxDepth = 15,
        bool includeTests = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);

        var fullSolutionPath = Path.GetFullPath(solutionPath);
        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(fullSolutionPath, cancellationToken: cancellationToken);
        var projectNamesByFilePath = AnalysisWorkspaceHelpers.BuildProjectNamesByFilePath(solution);
        var query = TypeSymbolResolutionHelper.ParseQuery(symbol);
        var candidates = await TypeSymbolResolutionHelper.FindCandidatesAsync(solution, query, projectNamesByFilePath, cancellationToken);

        if (candidates.Count == 0)
        {
            return CreateEmptyResponse(fullSolutionPath, symbol, property, access, declaration: null);
        }

        if (candidates.Count > 1)
        {
            throw new InvalidOperationException(TypeSymbolResolutionHelper.BuildAmbiguousSymbolMessage(symbol, candidates));
        }

        var candidate = candidates[0];
        var propertySymbol = candidate.Symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(member => string.Equals(member.Name, property, StringComparison.OrdinalIgnoreCase));

        if (propertySymbol is null)
        {
            return CreateEmptyResponse(fullSolutionPath, symbol, property, access, declaration: null);
        }

        var declaration = BuildDeclaration(propertySymbol, candidate);
        var testProjectPaths = includeTests
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : CallChainTraceHelper.BuildTestProjectPaths(solution);

        var accessesToTrace = ResolveRequestedAccesses(access, propertySymbol);
        var accessChains = new List<PropertyAccessCallChainsDto>();

        foreach (var accessKind in accessesToTrace)
        {
            var chains = await BuildAccessChainsAsync(
                solution,
                propertySymbol,
                accessKind,
                projectNamesByFilePath,
                testProjectPaths,
                maxDepth,
                cancellationToken);

            accessChains.Add(new PropertyAccessCallChainsDto
            {
                Access = accessKind,
                CallChains = chains,
                EntryPointCount = CallChainTraceHelper.CountEntryPoints(chains)
            });
        }

        return new TracePropertyCallersResponseDto
        {
            SolutionPath = fullSolutionPath,
            Symbol = symbol,
            Property = property,
            Access = access,
            Declaration = declaration,
            AccessChains = accessChains
        };
    }

    private static TracePropertyCallersResponseDto CreateEmptyResponse(
        string solutionPath,
        string symbol,
        string property,
        PropertyAccessKindDto access,
        TracePropertyCallersDeclarationDto? declaration)
    {
        return new TracePropertyCallersResponseDto
        {
            SolutionPath = solutionPath,
            Symbol = symbol,
            Property = property,
            Access = access,
            Declaration = declaration,
            AccessChains = Array.Empty<PropertyAccessCallChainsDto>()
        };
    }

    private static IReadOnlyList<PropertyAccessKindDto> ResolveRequestedAccesses(PropertyAccessKindDto access, IPropertySymbol propertySymbol)
    {
        return access switch
        {
            PropertyAccessKindDto.Get => propertySymbol.GetMethod is null ? Array.Empty<PropertyAccessKindDto>() : [PropertyAccessKindDto.Get],
            PropertyAccessKindDto.Set => propertySymbol.SetMethod is null ? Array.Empty<PropertyAccessKindDto>() : [PropertyAccessKindDto.Set],
            _ => BuildBothAccesses(propertySymbol)
        };
    }

    private static IReadOnlyList<PropertyAccessKindDto> BuildBothAccesses(IPropertySymbol propertySymbol)
    {
        var result = new List<PropertyAccessKindDto>(2);
        if (propertySymbol.GetMethod is not null)
        {
            result.Add(PropertyAccessKindDto.Get);
        }

        if (propertySymbol.SetMethod is not null)
        {
            result.Add(PropertyAccessKindDto.Set);
        }

        return result;
    }

    private static async Task<IReadOnlyList<CallChainNodeDto>> BuildAccessChainsAsync(
        Solution solution,
        IPropertySymbol propertySymbol,
        PropertyAccessKindDto access,
        IReadOnlyDictionary<string, string> projectNamesByFilePath,
        HashSet<string> testProjectPaths,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        var referenceGroups = await SymbolFinder.FindReferencesAsync(propertySymbol, solution, cancellationToken);
        var result = new List<CallChainNodeDto>();

        foreach (var referenceGroup in referenceGroups)
        {
            foreach (var location in referenceGroup.Locations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!location.Location.IsInSource || location.Document is null)
                {
                    continue;
                }

                if (location.Document.Project.FilePath is not null && testProjectPaths.Contains(location.Document.Project.FilePath))
                {
                    continue;
                }

                var classification = await ClassifyAccessAsync(location, propertySymbol, cancellationToken);
                if (!MatchesRequestedAccess(classification, access))
                {
                    continue;
                }

                var containingMethod = await ResolveContainingMethodAsync(location, propertySymbol, cancellationToken);
                if (containingMethod is null)
                {
                    continue;
                }

                var callNode = await BuildRootNodeAsync(
                    solution,
                    location,
                    containingMethod,
                    projectNamesByFilePath,
                    testProjectPaths,
                    maxDepth,
                    cancellationToken);

                if (callNode is not null)
                {
                    result.Add(callNode);
                }
            }
        }

        return result
            .OrderBy(item => item.Project, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Line)
            .ThenBy(item => item.Column)
            .ThenBy(item => item.CallSite.Line)
            .ThenBy(item => item.CallSite.Column)
            .ToArray();
    }

    private static bool MatchesRequestedAccess(PropertyAccessKindDto classification, PropertyAccessKindDto requestedAccess)
    {
        return classification == requestedAccess ||
               classification == PropertyAccessKindDto.Both ||
               requestedAccess == PropertyAccessKindDto.Both;
    }

    private static async Task<PropertyAccessKindDto> ClassifyAccessAsync(
        ReferenceLocation location,
        IPropertySymbol propertySymbol,
        CancellationToken cancellationToken)
    {
        var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await location.Document.GetSemanticModelAsync(cancellationToken);
        if (root is null || semanticModel is null)
        {
            return PropertyAccessKindDto.Get;
        }

        var referenceNode = GetReferenceExpression(root.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true));
        if (referenceNode is null)
        {
            return PropertyAccessKindDto.Get;
        }

        if (referenceNode.Parent is AssignmentExpressionSyntax assignment && assignment.Left == referenceNode)
        {
            return assignment.Kind() == SyntaxKind.SimpleAssignmentExpression
                ? PropertyAccessKindDto.Set
                : PropertyAccessKindDto.Both;
        }

        if ((referenceNode.Parent is PrefixUnaryExpressionSyntax prefix && prefix.Operand == referenceNode &&
             (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression))) ||
            (referenceNode.Parent is PostfixUnaryExpressionSyntax postfix && postfix.Operand == referenceNode &&
             (postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression))))
        {
            return PropertyAccessKindDto.Both;
        }

        if (referenceNode.Parent is ArgumentSyntax or ReturnStatementSyntax or InterpolationSyntax or ConditionalExpressionSyntax)
        {
            return PropertyAccessKindDto.Get;
        }

        var operation = semanticModel.GetOperation(referenceNode, cancellationToken);
        if (operation is null && referenceNode is ExpressionSyntax expression)
        {
            operation = semanticModel.GetOperation(expression, cancellationToken);
        }

        return operation switch
        {
            Microsoft.CodeAnalysis.Operations.ICompoundAssignmentOperation => PropertyAccessKindDto.Both,
            Microsoft.CodeAnalysis.Operations.IIncrementOrDecrementOperation => PropertyAccessKindDto.Both,
            _ => PropertyAccessKindDto.Get
        };
    }

    private static async Task<IMethodSymbol?> ResolveContainingMethodAsync(
        ReferenceLocation location,
        IPropertySymbol propertySymbol,
        CancellationToken cancellationToken)
    {
        var semanticModel = await location.Document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel is null)
        {
            return null;
        }

        var enclosingSymbol = semanticModel.GetEnclosingSymbol(location.Location.SourceSpan.Start, cancellationToken);
        if (enclosingSymbol is IMethodSymbol methodSymbol)
        {
            return methodSymbol;
        }

        return propertySymbol.GetMethod;
    }

    private static async Task<CallChainNodeDto?> BuildRootNodeAsync(
        Solution solution,
        ReferenceLocation location,
        IMethodSymbol containingMethod,
        IReadOnlyDictionary<string, string> projectNamesByFilePath,
        HashSet<string> testProjectPaths,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        if (!location.Location.IsInSource || location.Location.SourceTree is null)
        {
            return null;
        }

        var callFilePath = location.Location.SourceTree.FilePath;
        if (string.IsNullOrWhiteSpace(callFilePath))
        {
            return null;
        }

        var callPosition = location.Location.GetLineSpan().StartLinePosition;
        var callLine = callPosition.Line + 1;
        var callColumn = callPosition.Character + 1;

        if (!AnalysisWorkspaceHelpers.TryGetPrimaryLocation(containingMethod, out var filePath, out var line, out var column))
        {
            filePath = callFilePath;
            line = callLine;
            column = callColumn;
        }

        var projectName = AnalysisWorkspaceHelpers.ResolveDeclaringProjectName(
            solution,
            location.Document.Project,
            callFilePath,
            projectNamesByFilePath);

        var (callCondition, branch) = await CallChainTraceHelper.ExtractCallConditionAsync(location.Location, cancellationToken);
        var calledBy = await CallChainTraceHelper.FindCallersRecursiveAsync(
            solution,
            containingMethod,
            projectNamesByFilePath,
            testProjectPaths,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                containingMethod.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            },
            depth: 0,
            maxDepth: maxDepth,
            cancellationToken: cancellationToken);

        return new CallChainNodeDto
        {
            ContainingType = containingMethod.ContainingType?.Name ?? string.Empty,
            FullyQualifiedContainingType = containingMethod.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Method = containingMethod.Name,
            Project = projectName,
            FilePath = filePath,
            Line = line,
            Column = column,
            CallSite = new CallSiteDto
            {
                FilePath = callFilePath,
                Line = callLine,
                Column = callColumn,
                CallCondition = callCondition,
                Branch = branch
            },
            IsEntryPoint = calledBy.Count == 0,
            CalledBy = calledBy
        };
    }

    private static SyntaxNode? GetReferenceExpression(SyntaxNode? node)
    {
        if (node is null)
        {
            return null;
        }

        return node.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess when memberAccess.Name == node => memberAccess,
            MemberBindingExpressionSyntax memberBinding when memberBinding.Name == node => memberBinding,
            ConditionalAccessExpressionSyntax conditionalAccess when conditionalAccess.WhenNotNull == node => conditionalAccess,
            _ => node
        };
    }

    private static TracePropertyCallersDeclarationDto BuildDeclaration(IPropertySymbol propertySymbol, TypeSymbolCandidate candidate)
    {
        AnalysisWorkspaceHelpers.GetPreferredLocation(propertySymbol, out var filePath, out var line, out var column);

        return new TracePropertyCallersDeclarationDto
        {
            Symbol = candidate.Symbol.Name,
            Property = propertySymbol.Name,
            FullyQualifiedTypeName = candidate.FullyQualifiedName,
            Kind = candidate.Kind,
            Project = candidate.Project,
            FilePath = filePath,
            Line = line,
            Column = column,
            HasGetter = propertySymbol.GetMethod is not null,
            HasSetter = propertySymbol.SetMethod is not null
        };
    }
}
