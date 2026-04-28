using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeIntel.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeIntel.Analysis;

/// <summary>
/// Реализует поиск явных DI-регистраций ASP.NET Core и Castle Windsor внутри решения.
/// </summary>
public sealed class FindRegistrationsService : IFindRegistrationsService
{
    private static readonly SymbolDisplayFormat RegistrationSymbolDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>
    /// Инициализирует сервис поиска регистраций.
    /// </summary>
    public FindRegistrationsService()
    {
    }

    /// <inheritdoc />
    public async Task<FindRegistrationsResponseDto> FindAsync(
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
            return new FindRegistrationsResponseDto
            {
                SolutionPath = fullSolutionPath,
                Symbol = symbol,
                Declaration = null,
                Registrations = Array.Empty<FindRegistrationsResultDto>(),
                RegistrationCount = 0
            };
        }

        if (candidates.Count > 1)
        {
            throw new InvalidOperationException(TypeSymbolResolutionHelper.BuildAmbiguousSymbolMessage(symbol, candidates));
        }

        var candidate = candidates[0];
        var declaration = new FindRegistrationsDeclarationDto
        {
            Symbol = candidate.Symbol.Name,
            FullyQualifiedName = candidate.FullyQualifiedName,
            Kind = candidate.Kind,
            Project = candidate.Project,
            FilePath = candidate.FilePath,
            Line = candidate.Line,
            Column = candidate.Column
        };

        var registrations = await FindRegistrationLocationsAsync(
            candidate.Symbol,
            solution,
            projectNamesByFilePath,
            cancellationToken);

        return new FindRegistrationsResponseDto
        {
            SolutionPath = fullSolutionPath,
            Symbol = symbol,
            Declaration = declaration,
            Registrations = registrations,
            RegistrationCount = registrations.Count
        };
    }

    private static async Task<IReadOnlyList<FindRegistrationsResultDto>> FindRegistrationLocationsAsync(
        INamedTypeSymbol declaration,
        Solution solution,
        IReadOnlyDictionary<string, string> projectNamesByFilePath,
        CancellationToken cancellationToken)
    {
        var results = new List<FindRegistrationsResultDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targetType = declaration.OriginalDefinition;

        foreach (var project in solution.Projects.OrderBy(p => p.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var document in project.Documents.OrderBy(d => d.FilePath ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
                if (syntaxRoot is null)
                {
                    continue;
                }

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                if (semanticModel is null)
                {
                    continue;
                }

                foreach (var invocation in syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (TryCreateAspNetCoreRegistration(invocation, semanticModel, out var aspRegistration) &&
                        MatchesTarget(targetType, aspRegistration))
                    {
                        AddResult(results, seen, solution, project, document, projectNamesByFilePath, aspRegistration);
                    }

                    if (TryCreateCastleWindsorRegistrations(invocation, semanticModel, out var windsorRegistrations))
                    {
                        foreach (var windsorRegistration in windsorRegistrations)
                        {
                            if (MatchesTarget(targetType, windsorRegistration))
                            {
                                AddResult(results, seen, solution, project, document, projectNamesByFilePath, windsorRegistration);
                            }
                        }
                    }
                }
            }
        }

        return results
            .OrderBy(item => item.Project, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Line)
            .ThenBy(item => item.Column)
            .ToArray();
    }

    private static bool TryCreateAspNetCoreRegistration(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out RegistrationCandidate registration)
    {
        registration = default;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        var lifetime = methodName switch
        {
            "AddTransient" => "Transient",
            "AddScoped" => "Scoped",
            "AddSingleton" => "Singleton",
            _ => null
        };

        if (lifetime is null)
        {
            return false;
        }

        if (!TryResolveAspNetCoreTypes(invocation, semanticModel, out var serviceType, out var implementationType))
        {
            return false;
        }

        registration = CreateRegistrationCandidate(
            serviceType,
            implementationType,
            lifetime,
            RegistrationFrameworkDto.AspNetCoreDI,
            invocation);
        return true;
    }

    private static bool TryResolveAspNetCoreTypes(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out INamedTypeSymbol serviceType,
        out INamedTypeSymbol implementationType)
    {
        serviceType = null!;
        implementationType = null!;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (memberAccess.Name is GenericNameSyntax genericName &&
            genericName.TypeArgumentList.Arguments.Count == 2)
        {
            return TryGetNamedTypeSymbol(semanticModel, genericName.TypeArgumentList.Arguments[0], out serviceType) &&
                   TryGetNamedTypeSymbol(semanticModel, genericName.TypeArgumentList.Arguments[1], out implementationType);
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return false;
        }

        return TryGetNamedTypeFromTypeOf(arguments[0].Expression, semanticModel, out serviceType) &&
               TryGetNamedTypeFromTypeOf(arguments[1].Expression, semanticModel, out implementationType);
    }

    private static bool TryCreateCastleWindsorRegistrations(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out IReadOnlyList<RegistrationCandidate> registrations)
    {
        registrations = Array.Empty<RegistrationCandidate>();

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            !string.Equals(memberAccess.Name.Identifier.ValueText, "Register", StringComparison.Ordinal))
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        var result = new List<RegistrationCandidate>();
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (TryCreateCastleWindsorRegistration(argument.Expression, semanticModel, out var registration))
            {
                result.Add(registration);
            }
        }

        registrations = result;
        return result.Count > 0;
    }

    private static bool TryCreateCastleWindsorRegistration(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        out RegistrationCandidate registration)
    {
        registration = default;

        if (!TryFlattenInvocationChain(expression, out var operations))
        {
            return false;
        }

        var forOperation = operations.FirstOrDefault(static op => string.Equals(op.Name, "For", StringComparison.Ordinal));
        var implementedByOperation = operations.FirstOrDefault(static op => string.Equals(op.Name, "ImplementedBy", StringComparison.Ordinal));
        if (forOperation.NameSyntax is null || implementedByOperation.NameSyntax is null)
        {
            return false;
        }

        if (!TryResolveWindsorType(forOperation, semanticModel, out var serviceType) ||
            !TryResolveWindsorType(implementedByOperation, semanticModel, out var implementationType))
        {
            return false;
        }

        var lifetime = operations
            .Select(static op => op.Name)
            .Select(MapCastleLifestyle)
            .FirstOrDefault(static value => value is not null);

        registration = CreateRegistrationCandidate(
            serviceType,
            implementationType,
            lifetime,
            RegistrationFrameworkDto.CastleWindsor,
            expression);
        return true;
    }

    private static RegistrationCandidate CreateRegistrationCandidate(
        INamedTypeSymbol serviceType,
        INamedTypeSymbol implementationType,
        string? lifetime,
        RegistrationFrameworkDto framework,
        SyntaxNode syntaxNode)
    {
        var span = syntaxNode.GetLocation().GetLineSpan().StartLinePosition;

        return new RegistrationCandidate(
            serviceType.OriginalDefinition,
            implementationType.OriginalDefinition,
            FormatRegistrationType(serviceType),
            FormatRegistrationType(implementationType),
            lifetime,
            framework,
            span.Line + 1,
            span.Character + 1);
    }

    private static string FormatRegistrationType(INamedTypeSymbol type)
    {
        return type.OriginalDefinition.ToDisplayString(RegistrationSymbolDisplayFormat);
    }

    private static string? MapCastleLifestyle(string methodName)
    {
        return methodName switch
        {
            "LifestyleTransient" => "Transient",
            "LifestylePerWebRequest" => "PerWebRequest",
            "LifestylePerWcfOperation" => "PerWcfOperation",
            _ => null
        };
    }

    private static bool TryFlattenInvocationChain(
        ExpressionSyntax expression,
        out IReadOnlyList<InvocationOperation> operations)
    {
        var result = new List<InvocationOperation>();
        operations = result;

        if (!TryFlattenInvocationChainCore(expression, result))
        {
            operations = Array.Empty<InvocationOperation>();
            return false;
        }

        return result.Count > 0;
    }

    private static bool TryFlattenInvocationChainCore(ExpressionSyntax expression, List<InvocationOperation> operations)
    {
        switch (expression)
        {
            case InvocationExpressionSyntax invocation:
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    return false;
                }

                if (!TryFlattenInvocationChainCore(memberAccess.Expression, operations))
                {
                    if (memberAccess.Expression is not IdentifierNameSyntax)
                    {
                        return false;
                    }
                }

                operations.Add(new InvocationOperation(memberAccess.Name, invocation.ArgumentList.Arguments));
                return true;

            case IdentifierNameSyntax:
                return true;

            default:
                return false;
        }
    }

    private static bool TryResolveWindsorType(
        InvocationOperation operation,
        SemanticModel semanticModel,
        out INamedTypeSymbol type)
    {
        type = null!;

        if (operation.NameSyntax is GenericNameSyntax genericName &&
            genericName.TypeArgumentList.Arguments.Count == 1)
        {
            return TryGetNamedTypeSymbol(semanticModel, genericName.TypeArgumentList.Arguments[0], out type);
        }

        if (operation.Arguments.Count != 1)
        {
            return false;
        }

        return TryGetNamedTypeFromTypeOf(operation.Arguments[0].Expression, semanticModel, out type);
    }

    private static bool TryGetNamedTypeFromTypeOf(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        out INamedTypeSymbol type)
    {
        type = null!;

        if (expression is not TypeOfExpressionSyntax typeOfExpression)
        {
            return false;
        }

        return TryGetNamedTypeSymbol(semanticModel, typeOfExpression.Type, out type);
    }

    private static bool TryGetNamedTypeSymbol(
        SemanticModel semanticModel,
        TypeSyntax typeSyntax,
        out INamedTypeSymbol type)
    {
        type = null!;
        var symbolInfo = semanticModel.GetSymbolInfo(typeSyntax).Symbol;
        if (symbolInfo is INamedTypeSymbol namedType)
        {
            type = namedType;
            return true;
        }

        var typeInfo = semanticModel.GetTypeInfo(typeSyntax).Type;
        if (typeInfo is INamedTypeSymbol infoType)
        {
            type = infoType;
            return true;
        }

        return false;
    }

    private static bool MatchesTarget(INamedTypeSymbol targetType, RegistrationCandidate registration)
    {
        return SymbolEqualityComparer.Default.Equals(targetType, registration.ServiceType) ||
               SymbolEqualityComparer.Default.Equals(targetType, registration.ImplementationType);
    }

    private static void AddResult(
        List<FindRegistrationsResultDto> results,
        HashSet<string> seen,
        Solution solution,
        Project fallbackProject,
        Document document,
        IReadOnlyDictionary<string, string> projectNamesByFilePath,
        RegistrationCandidate registration)
    {
        var filePath = document.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var key = $"{registration.Framework}|{registration.ServiceSymbol}|{registration.ImplementationSymbol}|{filePath}|{registration.Line}|{registration.Column}";
        if (!seen.Add(key))
        {
            return;
        }

        results.Add(new FindRegistrationsResultDto
        {
            ServiceSymbol = registration.ServiceSymbol,
            ImplementationSymbol = registration.ImplementationSymbol,
            Lifetime = registration.Lifetime,
            RegistrationFramework = registration.Framework,
            Project = AnalysisWorkspaceHelpers.ResolveDeclaringProjectName(
                solution,
                fallbackProject,
                filePath,
                projectNamesByFilePath),
            FilePath = filePath,
            Line = registration.Line,
            Column = registration.Column
        });
    }

    private readonly record struct InvocationOperation(
        SimpleNameSyntax NameSyntax,
        SeparatedSyntaxList<ArgumentSyntax> Arguments)
    {
        public string Name => NameSyntax.Identifier.ValueText;
    }

    private readonly record struct RegistrationCandidate(
        INamedTypeSymbol ServiceType,
        INamedTypeSymbol ImplementationType,
        string ServiceSymbol,
        string ImplementationSymbol,
        string? Lifetime,
        RegistrationFrameworkDto Framework,
        int Line,
        int Column);
}
