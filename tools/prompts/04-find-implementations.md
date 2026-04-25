Implement the next MVP use case: find-implementations.

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
Add a command that finds implementations of a C# interface or abstract class in a solution.

Requirements:
1. Add a find-implementations command to CodeIntel.Cli
2. Support arguments:
   - --solution
   - --symbol
3. MVP symbol resolution:
   - exact case-insensitive match by type name
   - if multiple matching declarations are found, return a clear error
4. MVP scope supports implementations for:
   - interface
   - abstract class
5. If the resolved symbol is not an interface or abstract class:
   - return a valid JSON response with declaration and empty implementations
   - do not fail unless there is an input or resolution error
6. Top-level JSON response should include:
   - solutionPath
   - symbol
   - declaration
   - implementations
   - implementationCount
7. Declaration should include:
   - symbol
   - fullyQualifiedName if available
   - kind
   - project
   - filePath
   - line
   - column
8. Each implementation result should include:
   - symbol
   - fullyQualifiedName if available
   - kind
   - project
   - filePath
   - line
   - column
9. Put DTOs in CodeIntel.Contracts
10. Put Roslyn analysis logic in CodeIntel.Analysis
11. CLI should only parse arguments, call the service, and serialize output
12. Add tests
13. Update docs/03-interfaces/cli.md with:
   - command usage
   - valid examples
   - one JSON example
   - note MVP limitations

Non-goals:
- DI registration lookup
- method implementations
- property implementations
- namespace lookup
- project-name lookup
- fuzzy matching
- impact analysis
- editing code

Expected behavior:
- If symbol is not found, return valid JSON with declaration = null, implementations = [], implementationCount = 0
- If symbol is ambiguous, return non-zero exit code and clear error to stderr
- Keep JSON schema stable and simple
