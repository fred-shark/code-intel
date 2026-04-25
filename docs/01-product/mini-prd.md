# Mini PRD

## Goal
Create a read-only semantic analysis tool for C#/.NET solutions that helps developers and AI agents understand code structure, dependencies, and impact of changes.

## MVP Scope
- solution_summary
- find_symbol
- find_references
- find_implementations
- analyze_impact
- find_registrations
- trace_callers

## Non-goals
- code editing
- refactoring
- deployment
- runtime/log analysis
- global company-wide graph
- replacing IDE navigation

## Success Criteria
The tool should answer these questions on a real solution:
1. Where is this symbol declared?
2. Who uses this symbol?
3. What implementations exist for this interface?
4. Which projects are affected by this change?
5. What is the approximate impact scope?
6. Where is this type registered in the DI container?
7. Through which entry points is this method reachable, and under what conditions?

## Output
CLI first, JSON output, MCP wrapper later.
