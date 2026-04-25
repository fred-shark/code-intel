# Architecture Overview

## Modules
- CodeIntel.Contracts: DTOs and public contracts
- CodeIntel.Loader: load solution/projects and extract metadata
- CodeIntel.Analysis: semantic analysis logic using Roslyn
- CodeIntel.Cache: in-memory and persistent cache
- CodeIntel.Cli: command-line interface

## Principles
- read-only
- Roslyn-based
- CLI first
- stable JSON output
- no business logic in CLI
