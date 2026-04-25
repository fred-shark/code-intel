# ADR-001: CLI first

## Status
Accepted

## Context
We need a fast MVP that is easy to run locally and easy to integrate with AI coding agents.

## Decision
The first version will be implemented as a local CLI tool with JSON output.

## Consequences
### Positive
- simple distribution
- easy integration with Codex / Claude Code / Cline
- no server infrastructure required

### Negative
- no warm long-lived process initially
- some repeated startup cost
