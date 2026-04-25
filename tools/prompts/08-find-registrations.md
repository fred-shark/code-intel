Extend the MVP find-registrations command with Castle Windsor explicit registration support.

Context:
- Follow docs/01-product/mini-prd.md
- Follow docs/02-architecture/architecture-overview.md
- Follow docs/03-interfaces/cli.md
- Follow docs/05-examples/acceptance-checklist.md
- CLI must remain thin
- Public DTOs must live in CodeIntel.Contracts
- Keep the tool read-only
- Commands return JSON by default
- Do not add features outside the requested scope

Current state:
- The repository already has an MVP find-registrations command for ASP.NET Core DI patterns
- We now want to extend it with Castle Windsor explicit registration support
- This is still MVP-level support, not full Windsor coverage

Goal:
Add support for detecting explicit Castle Windsor registrations in find-registrations, while keeping the implementation conservative and stable.

Requirements:
1. Extend find-registrations so it can detect these Castle Windsor explicit registration patterns:
   - container.Register(Component.For<TService>().ImplementedBy<TImplementation>())
   - container.Register(Component.For(typeof(TService)).ImplementedBy(typeof(TImplementation)))
   - open generic variants, for example:
     - Component.For(typeof(IRepositoryBase<>)).ImplementedBy(typeof(RepositoryBase<>))
     - Component.For(typeof(IRepositorySynonymBase<>)).ImplementedBy(typeof(RepositorySynonymBase<>))
2. Extract lifestyle when present, at least for these patterns:
   - LifestyleTransient()
   - LifestylePerWebRequest()
   - LifestylePerWcfOperation()
3. Continue to support existing ASP.NET Core DI registrations unchanged
4. Use the existing symbol resolution approach for MVP:
   - exact case-insensitive type-name matching
5. If the target symbol is the service type in an explicit Windsor registration, that registration should be returned
6. If the target symbol is the implementation type in an explicit Windsor registration, that registration should also be returned
7. For open generic registrations, preserve the generic type information as clearly as possible in the result
8. Keep the existing top-level JSON response shape stable:
   - solutionPath
   - symbol
   - declaration
   - registrations
   - registrationCount
9. Each registration result should continue to include at least:
   - serviceSymbol
   - implementationSymbol
   - lifetime
   - project
   - filePath
   - line
   - column
10. If necessary, add a small optional field to distinguish provider/source, for example:
   - registrationFramework: "AspNetCoreDI" | "CastleWindsor"
   Add such a field only if it improves clarity and does not create unnecessary complexity
11. Put DTO changes in CodeIntel.Contracts
12. Put analysis logic in CodeIntel.Analysis
13. Keep CLI thin: only parse args, call service, serialize JSON
14. Add tests
15. Update docs/03-interfaces/cli.md
16. Update docs/05-examples/acceptance-checklist.md

Castle Windsor scope for this task:
Support only explicit registrations where the service and implementation are directly visible in the fluent chain.

Examples that should be supported:
- container.Register(Component.For<ITradeExportLogRepository>().ImplementedBy<TradeExportLogRepository>().LifestyleTransient());
- container.Register(Component.For<IUnitOfWork>().ImplementedBy<UnitOfWork>().LifestylePerWebRequest());
- container.Register(Component.For<IDataRepositoryFactory>().ImplementedBy<DataRepositoryFactory>().LifestyleTransient());
- container.Register(Component.For(typeof(IRepositoryBase<>)).ImplementedBy(typeof(RepositoryBase<>)).Named("repositoryBase").LifestylePerWcfOperation());
- container.Register(Component.For(typeof(IRepositorySynonymBase<>)).ImplementedBy(typeof(RepositorySynonymBase<>)).Named("repositorySynonymBase").LifestylePerWcfOperation());

Non-goals:
1. Do not implement full Windsor convention registration expansion for patterns like:
   - Classes.FromAssembly(...).BasedOn<...>().WithService.FromInterface()
   - Classes.FromAssembly(...).Pick().WithService.DefaultInterfaces()
   - Classes.FromAssembly(...).Pick().WithService.FirstInterface()
2. Do not add Autofac support
3. Do not add Scrutor support
4. Do not add runtime container inspection
5. Do not add reflection-based registration discovery
6. Do not add support for every possible Windsor fluent API branch
7. Do not refactor unrelated areas

Behavior expectations:
1. If symbol is not found, return valid JSON with:
   - declaration = null
   - registrations = []
   - registrationCount = 0
2. If symbol resolution is ambiguous, return non-zero exit code and clear error to stderr
3. Unsupported Windsor patterns should be ignored rather than causing failures
4. Keep the implementation conservative and deterministic

Implementation guidance:
1. Prefer semantic or syntax-aware analysis over plain text matching where practical
2. Detect Windsor explicit registrations by analyzing invocation chains rooted in:
   - Component.For(...)
   - ImplementedBy(...)
   - LifestyleTransient / LifestylePerWebRequest / LifestylePerWcfOperation
3. Support both generic and typeof(...) forms
4. For matching target symbol against a registration:
   - match by service type
   - match by implementation type
5. Keep the code modular
6. Reuse existing registration analysis infrastructure where possible
7. Avoid creating Windsor-specific logic inside the CLI layer

Tests:
Add at least the following tests:
1. Happy-path test for:
   - Component.For<IService>().ImplementedBy<Service>().LifestyleTransient()
2. Happy-path test for:
   - Component.For(typeof(IService)).ImplementedBy(typeof(Service)).LifestylePerWebRequest()
3. Happy-path test for open generic explicit registration:
   - Component.For(typeof(IRepositoryBase<>)).ImplementedBy(typeof(RepositoryBase<>)).LifestylePerWcfOperation()
4. Symbol-not-found test
5. Test that unsupported Classes.FromAssembly(...) patterns are ignored and do not fail the command
6. Test that existing ASP.NET Core DI registrations still work after the change

Documentation:
Update docs/03-interfaces/cli.md to mention:
- supported ASP.NET Core DI patterns
- supported Castle Windsor explicit patterns
- unsupported Castle Windsor convention patterns for now

Update docs/05-examples/acceptance-checklist.md with practical manual acceptance examples for the newly supported Windsor explicit registrations.

Important:
Keep the implementation narrow. Do not attempt full Windsor support in this task.

Important constraint:
Support only explicit Castle Windsor registrations with directly visible service and implementation types. Ignore convention-based registrations for now.