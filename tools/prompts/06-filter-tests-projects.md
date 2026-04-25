Implement test-project filtering for MVP results.

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
Exclude test projects from results by default, while allowing them to be included explicitly.

Requirements:
1. Extend solution/project metadata so projects can be classified as test projects
2. Add an isTestProject flag to the project summary output used by solution-summary
3. Classify a project as a test project using simple MVP heuristics:
   - project name ends with ".Tests", or
   - project name contains ".Tests.", or
   - project file references any package from the test package allowlist below
4. Use the following test package allowlist:
   - Microsoft.NET.Test.Sdk
   - xunit
   - xunit.runner.visualstudio
   - NUnit
   - NUnit3TestAdapter
   - nunit.framework
   - MSTest.TestFramework
   - MSTest.TestAdapter
   - Microsoft.VisualStudio.QualityTools.UnitTestFramework
5. Keep package-name matching case-insensitive
6. Prefer keeping the test package allowlist in one place, not duplicated across multiple files
7. Update solution-summary output to include isTestProject for each project
8. Update find-implementations so that, by default, implementations from test projects are excluded
9. Add an optional flag to find-implementations:
   - --include-tests
10. When --include-tests is provided, include implementations from test projects
11. Update analyze-impact so that, by default, implementationCount and affectedProjects are computed using the filtered implementation set
12. Add an optional flag to analyze-impact:
   - --include-tests
13. When --include-tests is provided, analyze-impact should include test-project implementations and affected projects from tests
14. Keep CLI thin
15. Put DTO changes in CodeIntel.Contracts
16. Put project classification and filtering logic in Loader/Analysis, not in CLI
17. Add tests
18. Update docs/03-interfaces/cli.md
19. Update docs/05-examples/acceptance-checklist.md

Non-goals:
- filtering references from test projects in find-references
- filtering symbols from test projects in find-symbol
- configurable filtering rules
- additional output formats
- DI analysis
- MCP wrapper
- refactoring unrelated areas

Expected behavior:
- solution-summary includes isTestProject
- find-implementations excludes test projects by default
- find-implementations --include-tests includes them
- analyze-impact excludes test-project implementation impact by default
- analyze-impact --include-tests includes it
- JSON schema stays stable except for the intentional additions

Implementation guidance:
- Keep the heuristic simple and deterministic
- Prefer backward-compatible DTO extension
- Reuse existing project metadata loading where possible
- Do not refactor unrelated areas
- Add at least:
  - one test for project classification by name
  - one test for project classification by package reference
  - one test that find-implementations excludes test projects by default
  - one test that find-implementations --include-tests restores the full result set
  - one test that analyze-impact changes implementationCount/affectedProjects depending on --include-tests