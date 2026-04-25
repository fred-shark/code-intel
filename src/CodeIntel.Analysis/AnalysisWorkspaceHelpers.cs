using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CodeIntel.Contracts;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;

namespace CodeIntel.Analysis;

internal static class AnalysisWorkspaceHelpers
{
    private static int _msbuildRegistered;

    internal static void RegisterMsBuild()
    {
        if (Interlocked.Exchange(ref _msbuildRegistered, 1) == 0)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    internal static IReadOnlyDictionary<string, string> BuildProjectNamesByFilePath(Solution solution)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (string.IsNullOrWhiteSpace(document.FilePath) || result.ContainsKey(document.FilePath))
                {
                    continue;
                }

                result[document.FilePath] = ResolveProjectName(project);
            }
        }

        return result;
    }

    internal static string ResolveDeclaringProjectName(
        Solution solution,
        Project fallbackProject,
        string filePath,
        IReadOnlyDictionary<string, string> projectNamesByFilePath)
    {
        if (projectNamesByFilePath.TryGetValue(filePath, out var projectName))
        {
            return projectName;
        }

        var documentId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
        if (documentId is not null)
        {
            var project = solution.GetProject(documentId.ProjectId);
            if (project is not null)
            {
                return ResolveProjectName(project);
            }
        }

        return ResolveProjectName(fallbackProject);
    }

    internal static string ResolveProjectName(Project project)
    {
        if (!string.IsNullOrWhiteSpace(project.Name))
        {
            return project.Name;
        }

        if (!string.IsNullOrWhiteSpace(project.FilePath))
        {
            return Path.GetFileNameWithoutExtension(project.FilePath);
        }

        return "Unknown";
    }

    internal static bool TryMapSymbolKind(TypeKind typeKind, out SymbolKindDto symbolKind)
    {
        symbolKind = typeKind switch
        {
            TypeKind.Class => SymbolKindDto.Class,
            TypeKind.Interface => SymbolKindDto.Interface,
            TypeKind.Enum => SymbolKindDto.Enum,
            _ => default
        };

        return typeKind is TypeKind.Class or TypeKind.Interface or TypeKind.Enum;
    }

    internal static bool TryGetPrimaryLocation(ISymbol symbol, out string filePath, out int line, out int column)
    {
        var location = symbol.Locations.FirstOrDefault(loc => loc.IsInSource && loc.SourceTree is not null);
        if (location is null)
        {
            filePath = string.Empty;
            line = 0;
            column = 0;
            return false;
        }

        filePath = location.SourceTree!.FilePath;
        var position = location.GetLineSpan().StartLinePosition;
        line = position.Line + 1;
        column = position.Character + 1;
        return true;
    }

    internal static void GetPreferredLocation(ISymbol symbol, out string filePath, out int line, out int column)
    {
        if (TryGetPrimaryLocation(symbol, out filePath, out line, out column))
        {
            return;
        }

        filePath = BuildMetadataLocation(symbol);
        line = 0;
        column = 0;
    }

    internal static string BuildMetadataLocation(ISymbol symbol)
    {
        var assemblyName = symbol.ContainingAssembly?.Name;
        var symbolName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (!string.IsNullOrWhiteSpace(assemblyName))
        {
            return $"[metadata]/{assemblyName}/{symbolName}";
        }

        return $"[metadata]/{symbolName}";
    }

    internal static string BuildSymbolKey(ISymbol symbol, string filePath, int line, int column)
    {
        return $"{symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}|{filePath}|{line}|{column}";
    }
}
