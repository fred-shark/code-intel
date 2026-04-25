Implement the next MVP use case: find-symbol.

Context:
- Follow docs/01-product/mini-prd.md
- Follow docs/02-architecture/architecture-overview.md
- CLI must remain thin
- Public DTOs must live in CodeIntel.Contracts
- Keep the tool read-only
- Do not add features outside the requested scope
- MVP commands return JSON by default

Requirements:
1. Add a find-symbol command to CodeIntel.Cli
2. Support:
   - --solution
   - --name
3. Search C# symbols in the provided solution
4. First version should support:
   - class
   - interface
   - enum
5. Return up to 20 results
6. Each result should include:
   - symbol name
   - fully qualified name if available
   - kind
   - project
   - file path
   - line
   - column
7. Put DTOs in CodeIntel.Contracts
8. Put Roslyn analysis logic in CodeIntel.Analysis
9. Add tests
10. Update docs/03-interfaces/cli.md with command usage and a JSON example

Non-goals:
- method lookup
- property lookup
- fuzzy ranking improvements
- references
- implementations
