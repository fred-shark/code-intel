# CLI Interface

## `solution-summary`

Loads a `.sln` or `.slnx` file and returns JSON describing the projects in the solution.

### Usage

```bash
dotnet run --project src/CodeIntel.Cli -- solution-summary --solution <path-to-solution.sln|path-to-solution.slnx>
```

Backward-compatible positional form is also supported:

```bash
dotnet run --project src/CodeIntel.Cli -- solution-summary <path-to-solution.sln|path-to-solution.slnx>
```

### Options

- `--solution`: explicit path to the solution file

### Errors

- invalid input is written to `stderr`
- invalid solution path is written to `stderr`
- invalid input returns a non-zero exit code
- unknown options such as `--json` are written to `stderr`

### Output

The command writes JSON to `stdout` and returns:
- `solutionPath`: absolute path to the loaded solution
- `projects`: list of projects in the solution
- `projects[].name`: project name from the solution entry when available, otherwise the project file name
- `projects[].path`: absolute path to the project file
- `projects[].targetFrameworks`: target frameworks declared in the project file
- `projects[].isTestProject`: whether the project is classified as a test project by MVP heuristics

### Example

```json
{
  "solutionPath": "/repo/DotnetCodeIntel.slnx",
  "projects": [
    {
      "name": "CodeIntel.Cli",
      "path": "/repo/src/CodeIntel.Cli/CodeIntel.Cli.csproj",
      "targetFrameworks": [
        "net10.0"
      ],
      "isTestProject": false
    }
  ]
}
```

## `find-symbol`

Ищет объявления классов, интерфейсов и перечислений по имени в указанном решении и возвращает до 20 совпадений.

Текущая MVP-версия поддерживает short name и fully qualified type name. Сопоставление выполняется без учета регистра.

### Usage

```bash
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution <path-to-solution.sln|path-to-solution.slnx> --name <symbol-name>
```

### Options

- `--solution`: путь к файлу решения (обязательно).
- `--name`: short name или fully qualified имя искомого типа без учета регистра (обязательно).

### Matching Rules

- Поддерживаются только типы `class`, `interface` и `enum`.
- Если запрос не содержит `.` и не начинается с `global::`, выполняется поиск по точному short name типа, например `SolutionSummaryLoader`.
- Если запрос содержит `.` или начинается с `global::`, выполняется поиск по fully qualified type name.
- Префикс `global::` нормализуется перед сравнением.
- Сопоставление выполняется без учета регистра, поэтому `solutionsummaryloader` и `global::codeintel.loader.solutionsummaryloader` считаются корректными lookup-ключами.
- Поиск по пространству имен не поддерживается.
- Поиск по имени проекта не поддерживается.

### Query Examples

Примеры запросов, которые считаются корректными для текущего MVP:

```bash
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name SolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name ISolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name solutionsummaryloader
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name CodeIntel.Loader.SolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name global::CodeIntel.Loader.SolutionSummaryLoader
```

Примеры запросов, которые не поддерживаются и не должны использоваться как lookup-ключ:

```bash
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name CodeIntel.Loader
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name CodeIntel.Loader.csproj
```

### Errors

- Пустой или отсутствующий путь к решению или имя символа выводится в `stderr`.
- Неизвестные `--`-опции также попадают в `stderr`.
- CLI возвращает код 1 при ошибках и выводит стандартное сообщение в `stderr`.

### Output

JSON с метаданными запроса и массивом результатов:

```json
{
  "solutionPath": "/repo/DotnetCodeIntel.slnx",
  "name": "Target",
  "results": [
    {
      "name": "Target",
      "fullyQualifiedName": "global::Sample.ClassSymbols.Target",
      "kind": "Class",
      "project": "App",
      "filePath": "/repo/src/App/ClassSymbol.cs",
      "line": 3,
      "column": 21
    }
  ]
}
```

## `find-references`

Ищет ссылки на объявление типа в указанном решении и возвращает JSON по умолчанию.

Текущий MVP поддерживает только типы `class`, `interface` и `enum`, умеет разрешать short name и fully qualified type name без учета регистра и не изменяет код.

### Usage

```bash
dotnet run --project src/CodeIntel.Cli -- find-references --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name>
```

### Options

- `--solution`: путь к файлу решения (обязательно).
- `--symbol`: short name или fully qualified имя искомого типа без учета регистра (обязательно).

### Valid Examples

```bash
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol solutionsummaryloader
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol CodeIntel.Loader.SolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol global::CodeIntel.Loader.SolutionSummaryLoader
```

### JSON Example

```json
{
  "solutionPath": "/repo/DotnetCodeIntel.slnx",
  "symbol": "SolutionSummaryLoader",
  "declaration": {
    "symbol": "SolutionSummaryLoader",
    "fullyQualifiedName": "global::CodeIntel.Loader.SolutionSummaryLoader",
    "kind": "Class",
    "project": "CodeIntel.Loader",
    "filePath": "/repo/src/CodeIntel.Loader/SolutionSummaryLoader.cs",
    "line": 5,
    "column": 21
  },
  "references": [
    {
      "symbol": "SolutionSummaryLoader",
      "referencedSymbolFullyQualifiedName": "global::CodeIntel.Loader.SolutionSummaryLoader",
      "project": "CodeIntel.Cli",
      "filePath": "/repo/src/CodeIntel.Cli/Program.cs",
      "line": 6,
      "column": 65
    }
  ],
  "referenceCount": 1
}
```

### Errors And Limitations

- Если символ не найден, команда возвращает JSON с `"declaration": null`, пустым массивом `"references"` и `"referenceCount": 0`.
- Если short-name запросу соответствует несколько объявлений типов, команда возвращает ненулевой код завершения и пишет понятную ошибку в `stderr`.
- Fully qualified запрос использует точное сопоставление по нормализованному полному имени и снимает неоднозначность между одноименными типами.
- Namespace-only lookup, project-name lookup, fuzzy matching и поиск по частичному совпадению не поддерживаются.
- Поиск ссылок на методы, свойства и анализ реализаций в этот MVP не входят.

## `find-implementations`

Ищет реализации интерфейса или абстрактного класса в указанном решении и возвращает JSON по умолчанию.

Текущий MVP ищет символ exact ignore-case по имени типа или по fully qualified имени. Ищутся реализации только для объявлений `interface` и `abstract class`; для остальных типов команда возвращает объявление и пустой список реализаций.

### Usage

```bash
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name> [--include-tests]
```

### Options

- `--solution`: путь к файлу решения (обязательно).
- `--symbol`: short name или fully qualified имя искомого типа без учета регистра (обязательно).
- `--include-tests`: включает реализации из проектов, классифицированных как тестовые.

### Matching Rules

- Интерфейс или абстрактный класс можно искать по точному short name без учета регистра.
- Fully qualified имя и `global::`-qualified имя также поддерживаются и сравниваются без учета регистра.
- Lookup по пространству имен как отдельной сущности и по имени проекта не поддерживается.
- По найденному объявлению ищутся реализации `interface` и наследники `abstract class`.
- По умолчанию реализации из test projects исключаются из результата.
- При `--include-tests` в результат снова попадают типы из test projects.

### Valid Examples

```bash
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol isolutionsummaryloader
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol CodeIntel.Loader.ISolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader --include-tests
```

## `analyze-impact`

Выполняет базовый анализ влияния изменений для type symbol в указанном решении и возвращает JSON по умолчанию.

Текущий MVP работает для short-name и fully qualified type lookup без учета регистра и использует детерминированные rule-based правила для `riskSummary`.

### Usage

```bash
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name> [--include-tests]
```

### Options

- `--solution`: путь к файлу решения (обязательно).
- `--symbol`: short name или fully qualified имя искомого типа без учета регистра (обязательно).
- `--include-tests`: включает влияние ссылок и реализаций из тестовых проектов в `referenceCount`, `implementationCount` и `affectedProjects`.

### Valid Examples

```bash
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol solutionsummaryloader
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol CodeIntel.Loader.ISolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader --include-tests
```

### JSON Example

```json
{
  "solutionPath": "/repo/DotnetCodeIntel.slnx",
  "symbol": "SolutionSummaryLoader",
  "declaration": {
    "symbol": "SolutionSummaryLoader",
    "fullyQualifiedName": "global::CodeIntel.Loader.SolutionSummaryLoader",
    "kind": "Class",
    "project": "CodeIntel.Loader",
    "filePath": "/repo/src/CodeIntel.Loader/SolutionSummaryLoader.cs",
    "line": 5,
    "column": 21
  },
  "referenceCount": 3,
  "implementationCount": 0,
  "affectedProjects": [
    "CodeIntel.Cli",
    "CodeIntel.Loader",
    "CodeIntel.Loader.Tests"
  ],
  "riskSummary": "High"
}
```

### Risk Rules

- `Unknown`: символ не найден.
- `Low`: ссылок и реализаций нет, затронут только проект объявления.
- `Medium`: символ найден, но не попадает в `Low` или `High`.
- `High`: затронуто не менее 3 проектов или найдено не менее 10 ссылок.

### Errors And Limitations

- Если символ не найден, команда возвращает JSON с `"declaration": null`, нулевыми счетчиками, пустым `"affectedProjects"` и `"riskSummary": "Unknown"`.
- Если short-name запросу соответствует несколько объявлений типов, команда возвращает ненулевой код завершения и пишет понятную ошибку в `stderr`.
- Поддерживаются только типы `class`, `interface`, `enum` и `abstract class`, если он уже разрешается текущим поиском типов.
- По умолчанию `referenceCount`, `implementationCount` и `affectedProjects` не учитывают ссылки и реализации из test projects; `--include-tests` включает их обратно.
- Namespace-only lookup, project-name lookup, fuzzy matching и анализ методов/свойств не поддерживаются.
- `riskSummary` вычисляется только по фиксированным MVP-правилам и не использует AI.

### JSON Example

```json
{
  "solutionPath": "/repo/DotnetCodeIntel.slnx",
  "symbol": "IHandler",
  "declaration": {
    "symbol": "IHandler",
    "fullyQualifiedName": "global::Sample.Abstractions.IHandler",
    "kind": "Interface",
    "project": "Abstractions",
    "filePath": "/repo/src/Abstractions/IHandler.cs",
    "line": 3,
    "column": 18
  },
  "implementations": [
    {
      "symbol": "ConsoleHandler",
      "fullyQualifiedName": "global::Sample.App.ConsoleHandler",
      "kind": "Class",
      "project": "App",
      "filePath": "/repo/src/App/ConsoleHandler.cs",
      "line": 5,
      "column": 21
    }
  ],
  "implementationCount": 1
}
```

### Errors And Limitations

- Если символ не найден, команда возвращает JSON с `"declaration": null`, пустым массивом `"implementations"` и `"implementationCount": 0`.
- Если найдено несколько объявлений типов с одинаковым именем, команда возвращает ненулевой код завершения и пишет понятную ошибку в `stderr`.
- Если найденный символ не является интерфейсом или абстрактным классом, команда не падает и возвращает объявление с пустым списком реализаций.
- По умолчанию test projects не участвуют в списке `implementations`; для включения нужен `--include-tests`.
- Namespace-only lookup, project-name lookup, fully-qualified-name lookup и fuzzy matching не поддерживаются.
- Поиск реализаций методов и свойств в этот MVP не входит.

## `find-registrations`

Ищет явные DI-регистрации для type symbol в указанном решении и возвращает JSON по умолчанию.

Текущий MVP ищет exact ignore-case совпадение по short name или fully qualified имени типа и поддерживает только registrations, где service и implementation напрямую видны в вызове.

### Usage

```bash
dotnet run --project src/CodeIntel.Cli -- find-registrations --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name>
```

### Options

- `--solution`: путь к файлу решения (обязательно).
- `--symbol`: short name или fully qualified имя искомого типа без учета регистра (обязательно).

### Supported ASP.NET Core DI Patterns

- `services.AddTransient<TService, TImplementation>()`
- `services.AddScoped<TService, TImplementation>()`
- `services.AddSingleton<TService, TImplementation>()`
- `services.AddTransient(typeof(TService), typeof(TImplementation))`
- `services.AddScoped(typeof(TService), typeof(TImplementation))`
- `services.AddSingleton(typeof(TService), typeof(TImplementation))`

### Supported Castle Windsor Explicit Patterns

- `container.Register(Component.For<TService>().ImplementedBy<TImplementation>())`
- `container.Register(Component.For(typeof(TService)).ImplementedBy(typeof(TImplementation)))`
- open generic explicit variants, for example:
  - `Component.For(typeof(IRepositoryBase<>)).ImplementedBy(typeof(RepositoryBase<>))`
  - `Component.For(typeof(IRepositorySynonymBase<>)).ImplementedBy(typeof(RepositorySynonymBase<>))`
- optional lifestyle extraction for:
  - `LifestyleTransient()`
  - `LifestylePerWebRequest()`
  - `LifestylePerWcfOperation()`

### Unsupported Castle Windsor Patterns For MVP

- `Classes.FromAssembly(...).BasedOn<...>().WithService.FromInterface()`
- `Classes.FromAssembly(...).Pick().WithService.DefaultInterfaces()`
- `Classes.FromAssembly(...).Pick().WithService.FirstInterface()`
- любые convention-based registrations, где service или implementation не видны прямо в fluent chain

### Matching Rules

- Символ ищется по short name или fully qualified имени типа без учета регистра.
- Для fully qualified запросов префикс `global::` нормализуется перед сравнением.
- Если целевой symbol совпадает с service type в явной регистрации, запись возвращается.
- Если целевой symbol совпадает с implementation type в явной регистрации, запись также возвращается.
- Для open generic registrations в `serviceSymbol` и `implementationSymbol` сохраняется generic-информация.

### JSON Example

```json
{
  "solutionPath": "/repo/DotnetCodeIntel.slnx",
  "symbol": "IService",
  "declaration": {
    "symbol": "IService",
    "fullyQualifiedName": "global::Sample.IService",
    "kind": "Interface",
    "project": "App",
    "filePath": "/repo/src/App/IService.cs",
    "line": 3,
    "column": 18
  },
  "registrations": [
    {
      "serviceSymbol": "global::Sample.IService",
      "implementationSymbol": "global::Sample.Service",
      "lifetime": "Transient",
      "registrationFramework": "CastleWindsor",
      "project": "App",
      "filePath": "/repo/src/App/CompositionRoot.cs",
      "line": 10,
      "column": 9
    }
  ],
  "registrationCount": 1
}
```

### Errors And Limitations

- Если символ не найден, команда возвращает JSON с `"declaration": null`, пустым массивом `"registrations"` и `"registrationCount": 0`.
- Если найдено несколько объявлений типов с одинаковым именем, команда возвращает ненулевой код завершения и пишет понятную ошибку в `stderr`.
- Неподдерживаемые Windsor convention patterns игнорируются и не приводят к падению команды.
- Namespace lookup, partial matching, runtime container inspection и reflection-based discovery не поддерживаются.

## `trace-callers`

Строит дерево вызовов для метода типа — от прямых вызывающих до точек входа (методов без вызывающих в пределах решения) — и возвращает JSON по умолчанию.

Текущий MVP поддерживает short name и fully qualified type name без учёта регистра, извлекает ближайший охватывающий `if`-guard на каждом шаге и обнаруживает точки входа как методы без вызывающих в решении.

### Usage

```bash
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name> --method <method-name> [--depth <n>] [--include-tests]
```

### Options

- `--solution`: путь к файлу решения (обязательно).
- `--symbol`: short name или fully qualified имя типа, содержащего метод (обязательно).
- `--method`: имя метода без учёта регистра (обязательно). При наличии нескольких перегрузок трейс строится для всех.
- `--depth`: максимальная глубина рекурсивного подъёма по вызовам. По умолчанию `15`.
- `--include-tests`: включать вызовы из проектов, классифицированных как тестовые.

### Matching Rules

- Тип ищется по exact short name или fully qualified имени без учёта регистра (аналогично `find-references`).
- Метод ищется по имени без учёта регистра на найденном типе. Поиск по сигнатуре не поддерживается.
- Если запросу соответствует несколько объявлений типа — команда возвращает ненулевой код и пишет ошибку в `stderr`.
- Если метод с таким именем не найден на типе — возвращается `declaration` с пустым `callChains`.
- Поиск вверх останавливается, когда у метода нет вызывающих в пределах решения (`isEntryPoint: true`).
- Точки входа, вызов которых происходит через интерфейс, находятся только если поиск ведётся по интерфейсному типу.

### Condition Extraction

На каждом шаге цепочки в `callSite` указывается ближайший охватывающий `if`-блок:
- `callCondition`: текст условия из исходного кода.
- `branch`: `"then"` — вызов в теле `if` (условие выполнилось); `"else"` — вызов в `else`-ветке (условие не выполнилось).
- Оба поля равны `null`, если вызов не находится внутри `if`-блока.

### Valid Examples

```bash
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader --method LoadAsync
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader --method LoadAsync
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader --method loadasync
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol CodeIntel.Loader.SolutionSummaryLoader --method LoadAsync
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader --method LoadAsync --depth 5
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader --method LoadAsync --include-tests
```

### JSON Example

```json
{
  "solutionPath": "/repo/DotnetCodeIntel.slnx",
  "symbol": "SolutionSummaryLoader",
  "method": "LoadAsync",
  "declaration": {
    "symbol": "SolutionSummaryLoader",
    "method": "LoadAsync",
    "fullyQualifiedTypeName": "global::CodeIntel.Loader.SolutionSummaryLoader",
    "kind": "Class",
    "project": "CodeIntel.Loader",
    "filePath": "/repo/src/CodeIntel.Loader/SolutionSummaryLoader.cs",
    "line": 18,
    "column": 29
  },
  "callChains": [
    {
      "containingType": "CliApplication",
      "fullyQualifiedContainingType": "global::CodeIntel.Cli.CliApplication",
      "method": "RunSolutionSummaryCommandAsync",
      "project": "CodeIntel.Cli",
      "filePath": "/repo/src/CodeIntel.Cli/CliApplication.cs",
      "line": 120,
      "column": 21,
      "callSite": {
        "filePath": "/repo/src/CodeIntel.Cli/CliApplication.cs",
        "line": 145,
        "column": 20,
        "callCondition": "!TryParseSolutionSummaryArguments(args, out var solutionPath, out var errorMessage)",
        "branch": "else"
      },
      "isEntryPoint": false,
      "calledBy": [
        {
          "containingType": "CliApplication",
          "fullyQualifiedContainingType": "global::CodeIntel.Cli.CliApplication",
          "method": "RunAsync",
          "project": "CodeIntel.Cli",
          "filePath": "/repo/src/CodeIntel.Cli/CliApplication.cs",
          "line": 90,
          "column": 25,
          "callSite": {
            "filePath": "/repo/src/CodeIntel.Cli/CliApplication.cs",
            "line": 100,
            "column": 20,
            "callCondition": "string.Equals(command, SolutionSummaryCommand, StringComparison.OrdinalIgnoreCase)",
            "branch": "then"
          },
          "isEntryPoint": false,
          "calledBy": [
            {
              "containingType": "Program",
              "fullyQualifiedContainingType": "global::Program",
              "method": "<Main>$",
              "project": "CodeIntel.Cli",
              "filePath": "/repo/src/CodeIntel.Cli/Program.cs",
              "line": 1,
              "column": 1,
              "callSite": {
                "filePath": "/repo/src/CodeIntel.Cli/Program.cs",
                "line": 5,
                "column": 21,
                "callCondition": null,
                "branch": null
              },
              "isEntryPoint": true,
              "calledBy": []
            }
          ]
        }
      ]
    }
  ],
  "entryPointCount": 1
}
```

### Errors And Limitations

## `trace-property-callers`

Строит дерево вызовов для чтения или записи свойства типа — от методов, где свойство используется, до точек входа — и возвращает JSON по умолчанию.

Текущий MVP поддерживает short name и fully qualified type name без учёта регистра, умеет отдельно трассировать `get` и `set`, извлекает ближайший охватывающий `if`-guard на каждом шаге и обнаруживает точки входа как методы без вызывающих в пределах решения.

### Usage

```bash
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name> --property <property-name> [--access <get|set|both>] [--depth <n>] [--include-tests]
```

### Options

- `--solution`: путь к файлу решения (обязательно).
- `--symbol`: short name или fully qualified имя типа, содержащего свойство (обязательно).
- `--property`: имя свойства без учёта регистра (обязательно).
- `--access`: режим трассировки доступа к свойству. Поддерживаются `get`, `set`, `both`. По умолчанию `both`.
- `--depth`: максимальная глубина рекурсивного подъёма по вызовам. По умолчанию `15`.
- `--include-tests`: включать чтения и записи из проектов, классифицированных как тестовые.

### Matching Rules

- Тип ищется по exact short name или fully qualified имени без учёта регистра.
- Свойство ищется по имени без учёта регистра на найденном типе.
- Если запросу соответствует несколько объявлений типа — команда возвращает ненулевой код и пишет ошибку в `stderr`.
- Режим `set` находит обычные присваивания и object initializer assignments.
- Режим `get` находит чтения свойства в выражениях, аргументах, return и других usage contexts.
- Операции вида `+=`, `++`, `--` и аналогичные read-modify-write попадают одновременно в `get` и `set`.
- Если свойство или нужный accessor не найден, команда возвращает `declaration` и пустые `accessChains` без ошибки.

### Condition Extraction

На каждом шаге цепочки в `callSite` указывается ближайший охватывающий `if`-блок:
- `callCondition`: текст условия из исходного кода.
- `branch`: `"then"` — использование свойства находится в теле `if`; `"else"` — в `else`-ветке.
- Оба поля равны `null`, если использование свойства не находится внутри `if`-блока.

### Valid Examples

```bash
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol FindReferencesResponseDto --property SolutionPath --access set
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol FindReferencesResponseDto --property SolutionPath --access get
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol FindReferencesResponseDto --property SolutionPath
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol CodeIntel.Contracts.FindReferencesResponseDto --property SolutionPath --access both
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol FindReferencesResponseDto --property SolutionPath --depth 5
```

### JSON Example

```json
{
  "solutionPath": "/repo/DotnetCodeIntel.slnx",
  "symbol": "FindReferencesResponseDto",
  "property": "SolutionPath",
  "access": "Set",
  "declaration": {
    "symbol": "FindReferencesResponseDto",
    "property": "SolutionPath",
    "fullyQualifiedTypeName": "global::CodeIntel.Contracts.FindReferencesResponseDto",
    "kind": "Class",
    "project": "CodeIntel.Contracts",
    "filePath": "/repo/src/CodeIntel.Contracts/FindReferencesResponseDto.cs",
    "line": 11,
    "column": 28,
    "hasGetter": true,
    "hasSetter": true
  },
  "accessChains": [
    {
      "access": "Set",
      "callChains": [
        {
          "containingType": "FindReferencesService",
          "fullyQualifiedContainingType": "global::CodeIntel.Analysis.FindReferencesService",
          "method": "FindAsync",
          "project": "CodeIntel.Analysis",
          "filePath": "/repo/src/CodeIntel.Analysis/FindReferencesService.cs",
          "line": 28,
          "column": 5,
          "callSite": {
            "filePath": "/repo/src/CodeIntel.Analysis/FindReferencesService.cs",
            "line": 81,
            "column": 13,
            "callCondition": null,
            "branch": null
          },
          "isEntryPoint": true,
          "calledBy": []
        }
      ],
      "entryPointCount": 1
    }
  ]
}
```

### Errors And Limitations

- Поиск строится от методов, содержащих использование свойства; accessor-методы `get_` и `set_` не показываются как отдельные корневые узлы.
- Если свойство не найдено на найденном типе, команда возвращает `declaration = null` и пустой массив `accessChains`.
- Если у свойства нет нужного accessor, `declaration` остаётся заполненным, но соответствующий bucket в `accessChains` отсутствует.

- Если тип не найден, команда возвращает JSON с `"declaration": null`, пустым `"callChains"` и `"entryPointCount": 0`.
- Если метод не найден на типе, возвращается `declaration` типа с пустым `"callChains"`.
- Если short-name запросу соответствует несколько объявлений типа, команда возвращает ненулевой код и пишет ошибку в `stderr`.
- По умолчанию вызовы из тестовых проектов не учитываются; `--include-tests` включает их.
- Если глубина рекурсии достигает `--depth`, подъём останавливается; такие узлы не помечаются как `isEntryPoint`.
- При наличии циклов в графе вызовов повторный обход одного метода не выполняется.
- Поиск по сигнатуре, namespace lookup, fuzzy matching и анализ вызовов через рефлексию не поддерживаются.
