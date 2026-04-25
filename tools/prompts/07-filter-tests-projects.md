Adjust analyze-impact so test-project filtering is applied consistently to references as well.

Context:
- analyze-impact riskSummary depends on referenceCount, implementationCount, and affectedProjectCount
- test-project implementations and affectedProjects are already filtered by default
- referenceCount must follow the same policy, otherwise riskSummary is inconsistent

Requirements:
1. In analyze-impact, when --include-tests is not provided, exclude references from test projects from the referenceCount used in the response and in riskSummary calculation
2. When --include-tests is provided, include test-project references again
3. Do not change the behavior of the standalone find-references command
4. Keep filtering policy inside analyze-impact orchestration/use-case logic
5. Update docs/03-interfaces/cli.md
6. Update docs/05-examples/acceptance-checklist.md
7. Add tests covering:
   - analyze-impact default filtering of test references
   - analyze-impact --include-tests restores full referenceCount
   - riskSummary changes accordingly when test references are excluded vs included

Non-goals:
- changing find-references output
- changing find-symbol behavior
- adding new output fields unless necessary