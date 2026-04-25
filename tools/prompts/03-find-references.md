Implement the next MVP use case: find-references.

Context:
- Follow docs/01-product/mini-prd.md
- Follow docs/02-architecture/architecture-overview.md
- Follow docs/03-interfaces/cli.md
- CLI must remain thin
- Public DTOs must live in CodeIntel.Contracts
- Keep the tool read-only
- Commands return JSON by default
- Do not add features outside the requested scope

Goal:
Add a command that finds references to a C# type symbol in a solution.

Requirements:
1. Add a find-references command to CodeIntel.Cli
2. Support arguments:
   - --solution
   - --symbol
3. The command should search references for a type symbol in the provided solution
4. MVP scope supports references for:
   - class
   - interface
   - enum
5. Symbol resolution for MVP:
   - first try exact case-insensitive match by type name
   - if multiple matching types are found, return a clear error in JSON
   - do not implement fuzzy matching
   - do not implement namespace-only or project-name lookup
6. For each reference result include:
   - symbol
   - referencedSymbolFullyQualifiedName if available
   - project
   - filePath
   - line
   - column
7. The top-level JSON response should include:
   - solutionPath
   - symbol
   - declaration
   - references
   - referenceCount
8. Declaration should include:
   - symbol
   - fullyQualifiedName if available
   - kind
   - project
   - filePath
   - line
   - column
9. Put public DTOs in CodeIntel.Contracts
10. Put Roslyn analysis logic in CodeIntel.Analysis
11. CLI should only parse arguments, call the service, and serialize output
12. Add tests
13. Update docs/03-interfaces/cli.md with:
   - command usage
   - valid examples
   - one JSON example
   - note current MVP limitations

Non-goals:
- method references
- property references
- namespace lookup
- project-name lookup
- partial matching
- implementations analysis
- impact analysis
- editing code
- runtime/log analysis

Expected behavior:
- If the symbol is not found, return a valid JSON response with declaration = null, references = [], referenceCount = 0
- If the symbol resolves to multiple candidate type declarations, return a non-zero exit code and print a clear error to stderr
- Keep JSON schema stable and simple

Suggested JSON shape:
{
  "solutionPath": "...",
  "symbol": "SolutionSummaryLoader",
  "declaration": {
    "symbol": "SolutionSummaryLoader",
    "fullyQualifiedName": "CodeIntel.Loader.SolutionSummaryLoader",
    "kind": "Class",
    "project": "CodeIntel.Loader",
    "filePath": "src/CodeIntel.Loader/SolutionSummaryLoader.cs",
    "line": 10,
    "column": 21
  },
  "references": [
    {
      "symbol": "SolutionSummaryLoader",
      "referencedSymbolFullyQualifiedName": "CodeIntel.Loader.SolutionSummaryLoader",
      "project": "CodeIntel.Cli",
      "filePath": "src/CodeIntel.Cli/CliApplication.cs",
      "line": 24,
      "column": 18
    }
  ],
  "referenceCount": 1
}

Implementation notes:
- Reuse existing solution loading logic where possible
- Reuse existing symbol-finding logic where possible
- Prefer clear module boundaries over clever shortcuts
- Add at least:
  - one happy-path test
  - one symbol-not-found test
  - one ambiguous-symbol test if practical

Keep the implementation conservative. Do not expand symbol resolution beyond exact type-name matching for MVP.