Implement the next MVP use case: analyze-impact.

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
Add a command that provides a basic impact analysis for a C# type symbol in a solution.

Requirements:
1. Add an analyze-impact command to CodeIntel.Cli
2. Support arguments:
   - --solution
   - --symbol
3. The command should reuse existing analysis services where possible
4. MVP scope supports symbols of type:
   - class
   - interface
   - enum
   - abstract class if already supported by existing symbol resolution
5. The top-level JSON response should include:
   - solutionPath
   - symbol
   - declaration
   - referenceCount
   - implementationCount
   - affectedProjects
   - riskSummary
6. Affected projects should be the distinct union of:
   - declaration project
   - projects containing references
   - projects containing implementations
7. riskSummary should be deterministic and rule-based, not AI-generated
8. Suggested risk rules for MVP:
   - Low: no references, no implementations, only declaration project affected
   - Medium: references or implementations exist, but affectedProjects count <= 2
   - High: affectedProjects count >= 3 or referenceCount is relatively high
   - Unknown: symbol not found or ambiguous resolution
9. Put public DTOs in CodeIntel.Contracts
10. Put analysis logic in CodeIntel.Analysis
11. CLI should only parse arguments, call the service, and serialize output
12. Add tests
13. Update docs/03-interfaces/cli.md with:
   - command usage
   - valid examples
   - one JSON example
   - note MVP limitations

Non-goals:
- method/property impact
- namespace lookup
- project-name lookup
- fuzzy matching
- DI graph analysis
- endpoint analysis
- runtime/log analysis
- editing code

Expected behavior:
- If symbol is not found, return valid JSON with declaration = null, referenceCount = 0, implementationCount = 0, affectedProjects = [], riskSummary = "Unknown"
- If symbol is ambiguous, return non-zero exit code and clear error to stderr
- Keep JSON schema stable and simple