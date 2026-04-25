# Чеклист приемки MVP

Документ описывает ручную проверку текущего MVP CLI.

## Общие условия

- Команды запускаются из корня репозитория
- Команды по умолчанию возвращают JSON
- В примерах используется PowerShell
- Базовое решение для проверки: `DotnetCodeIntel.slnx`

---

## 1. solution-summary

### Назначение
Проверить, что инструмент может загрузить solution и вернуть список проектов с базовой метаинформацией.

### Команда

```powershell
dotnet run --project src/CodeIntel.Cli -- solution-summary --solution DotnetCodeIntel.slnx
````

### Ожидаемый результат

* Возвращается валидный JSON
* В ответе есть `solutionPath`
* В ответе есть массив `projects`
* В `projects` присутствуют основные проекты из `src/` и тестовые проекты из `tests/`

### Что проверить в JSON

* `solutionPath` указывает на `DotnetCodeIntel.slnx`
* `projects` не пустой
* У каждого проекта есть:

  * `name`
  * `path`
  * `targetFrameworks`
  * `isTestProject`
* Для текущего MVP `targetFrameworks` должны содержать `net10.0`, если конфигурация не менялась
* Для проектов из `tests/` или проектов с тестовыми пакетами `isTestProject = true`

### Дополнительная проверка: invalid solution path

```powershell
dotnet run --project src/CodeIntel.Cli -- solution-summary --solution Missing.slnx
```

Ожидается:

* ненулевой код выхода
* ошибка выводится в stderr
* stdout не содержит успешный JSON

---

## 2. find-symbol

### Назначение

Проверить, что инструмент находит type symbols по short name и fully qualified имени без учета регистра.

### Happy path: класс

```powershell
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name SolutionSummaryLoader
```

### Happy path: интерфейс

```powershell
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name ISolutionSummaryLoader
```

### Проверка case-insensitive matching

```powershell
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name solutionsummaryloader
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name isolutionsummaryloader
```

### Проверка fully qualified lookup

```powershell
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name CodeIntel.Loader.SolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name global::CodeIntel.Loader.SolutionSummaryLoader
```

### Symbol not found

```powershell
dotnet run --project src/CodeIntel.Cli -- find-symbol --solution DotnetCodeIntel.slnx --name DefinitelyMissingType
```

### Что проверить в JSON

* В ответе есть:

  * `solutionPath`
  * `name`
  * `results`
* Для существующего символа:

  * `results` не пустой
  * у результата есть:

    * `symbol`
    * `fullyQualifiedName`
    * `kind`
    * `project`
    * `filePath`
    * `line`
    * `column`
* Для отсутствующего символа:

  * `results` — пустой массив

### Ограничения MVP

* Поддерживается поиск по short name и fully qualified имени типа
* Namespace-only lookup не поддерживается
* Project-name lookup не поддерживается
* Поддерживаются class / interface / enum

---

## 3. find-references

### Назначение

Проверить, что инструмент находит использования type symbol в solution.

### Happy path: класс

```powershell
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader
```

### Happy path: интерфейс

```powershell
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
```

### Symbol not found

```powershell
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol DefinitelyMissingType
```

### Проверка case-insensitive matching

```powershell
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol solutionsummaryloader
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol isolutionsummaryloader
```

### Проверка fully qualified lookup

```powershell
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol CodeIntel.Loader.SolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol global::CodeIntel.Loader.SolutionSummaryLoader
```

### Invalid solution path

```powershell
dotnet run --project src/CodeIntel.Cli -- find-references --solution Missing.slnx --symbol SolutionSummaryLoader
```

### Что проверить в JSON

* В ответе есть:

  * `solutionPath`
  * `symbol`
  * `declaration`
  * `references`
  * `referenceCount`
* Для найденного символа:

  * `declaration` не `null`
  * `referenceCount` равен длине массива `references`
* Для отсутствующего символа:

  * `declaration` = `null`
  * `references` = `[]`
  * `referenceCount` = `0`

### Ограничения MVP

* Поддерживаются только ссылки на type symbols
* Method/property references не поддерживаются
* Namespace-only lookup не поддерживается

---

## 4. find-implementations

### Назначение

Проверить, что инструмент находит реализации интерфейсов и abstract classes.

### Проверка concrete class

```powershell
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader
```

### Проверка интерфейса

```powershell
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
```

### Symbol not found

```powershell
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol DefinitelyMissingType
```

### Проверка case-insensitive matching

```powershell
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol isolutionsummaryloader
```

### Проверка fully qualified lookup

```powershell
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol CodeIntel.Loader.ISolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol global::CodeIntel.Loader.ISolutionSummaryLoader
```

### Проверка включения тестовых проектов

```powershell
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader --include-tests
```

### Что проверить в JSON

* В ответе есть:

  * `solutionPath`
  * `symbol`
  * `declaration`
  * `implementations`
  * `implementationCount`
* Для `SolutionSummaryLoader`:

  * `declaration` найден
  * `implementations` пустой массив
  * `implementationCount = 0`
* Для `ISolutionSummaryLoader`:

  * `declaration` найден
  * `implementationCount >= 1`
  * в `implementations` есть `SolutionSummaryLoader`
* Для отсутствующего символа:

  * `declaration` = `null`
  * `implementations` = `[]`
  * `implementationCount` = `0`

### Важная особенность MVP

* По умолчанию результаты не должны включать реализации из тестовых проектов
* При `--include-tests` результаты могут включать реализации из тестовых проектов

---

## 5. analyze-impact

### Назначение

Проверить, что инструмент собирает сводный impact analysis по типу.

### Проверка concrete class

```powershell
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader
```

### Проверка интерфейса

```powershell
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
```

### Symbol not found

```powershell
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol DefinitelyMissingType
```

### Проверка case-insensitive matching

```powershell
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol isolutionsummaryloader
```

### Проверка fully qualified lookup

```powershell
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol CodeIntel.Loader.ISolutionSummaryLoader
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol global::CodeIntel.Loader.ISolutionSummaryLoader
```

### Проверка включения тестовых проектов

```powershell
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader --include-tests
```

### Invalid solution path

```powershell
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution Missing.slnx --symbol ISolutionSummaryLoader
```

### Что проверить в JSON

* В ответе есть:

  * `solutionPath`
  * `symbol`
  * `declaration`
  * `referenceCount`
  * `implementationCount`
  * `affectedProjects`
  * `riskSummary`

### Отдельные проверки для analyze-impact

#### 1. Согласованность с find-references

Сначала отдельно выполни:

```powershell
dotnet run --project src/CodeIntel.Cli -- find-references --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
```

Потом:

```powershell
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
```

Проверь:

* `referenceCount` в `analyze-impact` совпадает с `referenceCount` из `find-references`
* По умолчанию `referenceCount` в `analyze-impact` может быть меньше, чем в standalone `find-references`, если часть ссылок находится в тестовых проектах
* При `--include-tests` `referenceCount` в `analyze-impact` должен снова совпасть с полным результатом standalone `find-references`

#### 2. Согласованность с find-implementations

Сначала отдельно выполни:

```powershell
dotnet run --project src/CodeIntel.Cli -- find-implementations --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
```

Потом:

```powershell
dotnet run --project src/CodeIntel.Cli -- analyze-impact --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
```

Проверь:

* `implementationCount` в `analyze-impact` совпадает с `implementationCount` из `find-implementations`
* При `--include-tests` оба значения увеличиваются или остаются равными, если тестовых реализаций нет

#### 3. Проверка affectedProjects

Для `ISolutionSummaryLoader` проверь, что `affectedProjects` содержит объединение проектов из:

* `declaration.project`
* всех `references[*].project`
* всех `implementations[*].project`
* По умолчанию проекты из тестовых реализаций не должны попадать в `affectedProjects`
* При `--include-tests` такие проекты должны появляться в `affectedProjects`

#### 4. Проверка riskSummary

Проверь, что `riskSummary` соответствует детерминированным правилам, принятым в MVP:

* `Low` — нет references, нет implementations, затронут только проект декларации
* `Medium` — есть references или implementations, но затронуто не более 2 проектов
* `High` — затронуто 3 и более проектов или referenceCount достаточно высокий по текущей логике
* `Unknown` — символ не найден или не удалось корректно разрешить
* При наличии ссылок только в test projects уровень риска по умолчанию должен снижаться относительно варианта с `--include-tests`

---

## 6. find-registrations

### Назначение

Проверить, что инструмент находит явные DI-регистрации для service и implementation type.

### ASP.NET Core DI: generic registration

```powershell
dotnet run --project src/CodeIntel.Cli -- find-registrations --solution DotnetCodeIntel.slnx --symbol ISolutionSummaryLoader
```

Ожидается:

* возвращается валидный JSON
* в ответе есть `solutionPath`, `symbol`, `declaration`, `registrations`, `registrationCount`
* у каждой найденной записи есть:
  * `serviceSymbol`
  * `implementationSymbol`
  * `lifetime`
  * `registrationFramework`
  * `project`
  * `filePath`
  * `line`
  * `column`

### Castle Windsor: explicit generic registration

Проверить на решении, где есть код вида:

```csharp
container.Register(Component.For<ITradeExportLogRepository>().ImplementedBy<TradeExportLogRepository>().LifestyleTransient());
```

Команда:

```powershell
dotnet run --project src/CodeIntel.Cli -- find-registrations --solution <path-to-solution> --symbol ITradeExportLogRepository
```

Ожидается:

* `registrationCount >= 1`
* хотя бы у одной записи `registrationFramework = "CastleWindsor"`
* `lifetime = "Transient"`
* `implementationSymbol` указывает на `TradeExportLogRepository`

### Castle Windsor: explicit typeof registration

Проверить на решении, где есть код вида:

```csharp
container.Register(Component.For(typeof(IUnitOfWork)).ImplementedBy(typeof(UnitOfWork)).LifestylePerWebRequest());
```

Команда:

```powershell
dotnet run --project src/CodeIntel.Cli -- find-registrations --solution <path-to-solution> --symbol UnitOfWork
```

Ожидается:

* регистрация находится и по implementation type
* `registrationFramework = "CastleWindsor"`
* `lifetime = "PerWebRequest"`

### Castle Windsor: open generic registration

Проверить на решении, где есть код вида:

```csharp
container.Register(Component.For(typeof(IRepositoryBase<>)).ImplementedBy(typeof(RepositoryBase<>)).Named("repositoryBase").LifestylePerWcfOperation());
```

Команда:

```powershell
dotnet run --project src/CodeIntel.Cli -- find-registrations --solution <path-to-solution> --symbol IRepositoryBase
```

Ожидается:

* `registrationFramework = "CastleWindsor"`
* `lifetime = "PerWcfOperation"`
* в `serviceSymbol` и `implementationSymbol` сохраняется generic-информация

### Unsupported Windsor convention pattern

Проверить на решении, где есть код вида:

```csharp
container.Register(Classes.FromAssembly(...).BasedOn<IUnitOfWork>().WithService.DefaultInterfaces());
```

Команда:

```powershell
dotnet run --project src/CodeIntel.Cli -- find-registrations --solution <path-to-solution> --symbol IUnitOfWork
```

Ожидается:

* команда не падает
* неподдерживаемая convention registration игнорируется

### Symbol not found

```powershell
dotnet run --project src/CodeIntel.Cli -- find-registrations --solution DotnetCodeIntel.slnx --symbol DefinitelyMissingType
```

Ожидается:

* `declaration = null`
* `registrations = []`
* `registrationCount = 0`

### Проверка fully qualified lookup

Если в решении есть два одноименных сервиса в разных namespace, выполнить:

```powershell
dotnet run --project src/CodeIntel.Cli -- find-registrations --solution <path-to-solution> --symbol Company.Product.Second.IService
dotnet run --project src/CodeIntel.Cli -- find-registrations --solution <path-to-solution> --symbol global::Company.Product.Second.IService
```

Ожидается:

* возвращаются только регистрации для выбранного fully qualified типа
* short-name неоднозначность снимается без изменения JSON-схемы

---

## 8. trace-callers

### Назначение

Проверить, что инструмент строит дерево вызовов от метода до точек входа с извлечением if-условий.

### Happy path: метод с одним вызывающим

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader --method LoadAsync
```

Ожидается:

* возвращается валидный JSON
* `declaration` не `null`
* `declaration.symbol` = `SolutionSummaryLoader`, `declaration.method` = `LoadAsync`
* `callChains` не пустой
* прямой вызывающий: `RunSolutionSummaryCommandAsync` в `CliApplication`
* в `calledBy` присутствует `RunAsync`, далее `<Main>$` из `Program`
* у `<Main>$` `isEntryPoint = true`, `calledBy = []`
* на шаге `RunAsync → RunSolutionSummaryCommandAsync` в `callSite.callCondition` есть `SolutionSummaryCommand`, `branch = "then"`
* `entryPointCount >= 1`

### Проверка case-insensitive поиска метода

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader --method loadasync
```

Ожидается: результат идентичен предыдущему.

### Проверка fully qualified lookup типа

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol CodeIntel.Loader.SolutionSummaryLoader --method LoadAsync
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol global::CodeIntel.Loader.SolutionSummaryLoader --method LoadAsync
```

Ожидается: тот же результат, что и при поиске по short name.

### Проверка --depth

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader --method LoadAsync --depth 1
```

Ожидается: `calledBy` на первом уровне не пустой, но дальнейшего подъёма нет.

### Symbol not found

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol DefinitelyMissingType --method SomeMethod
```

Ожидается:

* `declaration = null`
* `callChains = []`
* `entryPointCount = 0`

### Method not found

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader --method NonExistentMethod
```

Ожидается:

* `declaration` не `null` (тип найден)
* `callChains = []`
* `entryPointCount = 0`

### Missing required options

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-callers --solution DotnetCodeIntel.slnx --symbol SolutionSummaryLoader
```

Ожидается: ненулевой код выхода, ошибка про отсутствие `--method` в stderr.

### Что проверить в JSON

* В ответе есть: `solutionPath`, `symbol`, `method`, `declaration`, `callChains`, `entryPointCount`
* Каждый узел `callChains` содержит: `containingType`, `fullyQualifiedContainingType`, `method`, `project`, `filePath`, `line`, `column`, `callSite`, `isEntryPoint`, `calledBy`
* `callSite` содержит: `filePath`, `line`, `column`, `callCondition`, `branch`
* `callCondition` и `branch` равны `null` когда вызов не в `if`-блоке
* `isEntryPoint = true` только у методов без вызывающих в решении
* `entryPointCount` совпадает с числом узлов с `isEntryPoint = true` по всему дереву

---

## 9. trace-property-callers

### Назначение

Проверить, что инструмент строит дерево вызовов для чтений и записей свойства до точек входа с извлечением if-условий.

### Happy path: запись свойства через object initializer

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol FindReferencesResponseDto --property SolutionPath --access set
```

Ожидается:

* возвращается валидный JSON
* `declaration` не `null`
* `declaration.symbol` = `FindReferencesResponseDto`, `declaration.property` = `SolutionPath`
* `accessChains` не пустой
* существует bucket с `access = "Set"`
* в `callChains` есть метод, где выполняется присваивание свойства
* `entryPointCount >= 1`

### Happy path: чтение свойства

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol FindReferencesResponseDto --property SolutionPath --access get
```

Ожидается:

* существует bucket с `access = "Get"`
* в `callChains` присутствуют методы, где значение свойства читается

### Проверка fully qualified lookup типа

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol CodeIntel.Contracts.FindReferencesResponseDto --property SolutionPath --access both
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol global::CodeIntel.Contracts.FindReferencesResponseDto --property SolutionPath --access both
```

Ожидается: тот же результат, что и при поиске по short name.

### Проверка --depth

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol FindReferencesResponseDto --property SolutionPath --access set --depth 1
```

Ожидается: верхний уровень найден, но дальнейшего подъёма по `calledBy` нет.

### Property not found

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol FindReferencesResponseDto --property MissingProperty
```

Ожидается:

* `declaration = null`
* `accessChains = []`

### Missing required options

```powershell
dotnet run --project src/CodeIntel.Cli -- trace-property-callers --solution DotnetCodeIntel.slnx --symbol FindReferencesResponseDto
```

Ожидается: ненулевой код выхода, ошибка про отсутствие `--property` в stderr.

### Что проверить в JSON

* В ответе есть: `solutionPath`, `symbol`, `property`, `access`, `declaration`, `accessChains`
* `declaration` содержит: `symbol`, `property`, `fullyQualifiedTypeName`, `kind`, `project`, `filePath`, `line`, `column`, `hasGetter`, `hasSetter`
* Каждый элемент `accessChains` содержит: `access`, `callChains`, `entryPointCount`
* Каждый узел `callChains` содержит: `containingType`, `fullyQualifiedContainingType`, `method`, `project`, `filePath`, `line`, `column`, `callSite`, `isEntryPoint`, `calledBy`
* `callCondition` и `branch` равны `null`, когда использование свойства не находится в `if`-блоке

---

## 7. Базовая проверка build и test

Перед финальной приемкой выполнить:

```powershell
dotnet build DotnetCodeIntel.slnx
dotnet test DotnetCodeIntel.slnx
```

Ожидается:

* build проходит успешно
* test проходит успешно

---

## Критерии приемки MVP

MVP можно считать приемлемым, если выполняются все условия:

* `solution-summary` корректно читает solution и возвращает список проектов
* `find-symbol` находит type symbols по short name и fully qualified имени без учета регистра
* `find-references` возвращает declaration и корректный список ссылок
* `find-implementations` находит реализации интерфейсов и abstract classes
* `analyze-impact` согласован с `find-references` и `find-implementations`
* `find-registrations` находит поддерживаемые явные ASP.NET Core DI и Castle Windsor registrations
* `trace-callers` строит дерево вызовов до точек входа с корректными if-условиями
* `trace-property-callers` строит дерево вызовов для `get` и `set` свойства до точек входа
* для отсутствующего символа команды возвращают корректный пустой результат
* для невалидного пути к solution возвращается ошибка и ненулевой код выхода
* JSON-ответы имеют стабильную схему
* CLI остается thin layer без бизнес-логики анализа
