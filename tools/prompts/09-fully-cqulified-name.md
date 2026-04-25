Improve type symbol resolution across the CLI so fully qualified type names are supported.

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
Support resolving types by:
1. short name, e.g. Cart
2. fully qualified name, e.g. DataAccessLayer.Models.Cart
3. global-qualified name, e.g. global::DataAccessLayer.Models.Cart

Scope:
Apply this improvement consistently to all commands that resolve a type symbol:
- find-symbol
- find-references
- find-implementations
- find-registrations
- analyze-impact

Requirements:
1. Extend the existing type resolution logic so that:
   - if the input contains a dot "." or starts with "global::", treat it as a qualified type query
   - otherwise treat it as a short-name query
2. Normalize queries before comparison:
   - strip the "global::" prefix if present
   - compare case-insensitively for MVP consistency
3. For qualified queries, match against the type's fully qualified name
4. For short-name queries, keep the current exact case-insensitive short-name matching behavior
5. Preserve current ambiguity behavior:
   - if multiple candidate types match a short-name query, return a non-zero exit code and a clear error to stderr
6. For qualified queries:
   - prefer exact match on normalized fully qualified name
   - if no match exists, return the normal "not found" result for the command
7. Do not change the JSON output schema unless necessary
8. Keep CLI thin
9. Put resolution logic in the appropriate analysis layer, not in CLI
10. Reuse the same shared symbol resolution logic across all relevant commands where practical
11. Add tests
12. Update docs/03-interfaces/cli.md
13. Update docs/05-examples/acceptance-checklist.md

Non-goals:
- method-level fully qualified names
- property-level fully qualified names
- parsing generic argument expressions from user input
- assembly-qualified names
- nested type special syntax beyond current Roslyn-friendly naming if that complicates MVP
- fuzzy matching
- namespace-only lookup as a separate feature

Expected behavior examples:
- Cart -> short-name resolution
- DataAccessLayer.Models.Cart -> fully qualified type resolution
- global::DataAccessLayer.Models.Cart -> fully qualified type resolution after normalization

Add tests covering at least:
1. short-name resolution still works
2. fully qualified name resolution works
3. global-qualified name resolution works
4. ambiguous short-name still fails clearly
5. qualified query disambiguates correctly when multiple types share the same short name
6. the behavior is consistent across find-symbol, find-references, and analyze-impact at minimum

Implementation guidance:
- Keep the implementation conservative
- Prefer a shared normalization/resolution helper instead of duplicating logic in each command
- Do not refactor unrelated areas