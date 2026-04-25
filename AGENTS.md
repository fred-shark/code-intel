# Repository Instructions

- Все `public` классы, интерфейсы, структуры, записи, методы, свойства и другие публичные члены должны сопровождаться XML-комментариями на русском языке.
- Тесты запускать через `dotnet test` с отключением build server и shared compilation:
  `env DOTNET_CLI_USE_MSBUILDNOINPROCNODE=1 DOTNET_BUILD_SERVER_KEEPALIVE=0 dotnet test <path-to-csproj> --no-restore -p:UseSharedCompilation=false -maxcpucount:1`
- Для CLI-тестов использовать:
  `env DOTNET_CLI_USE_MSBUILDNOINPROCNODE=1 DOTNET_BUILD_SERVER_KEEPALIVE=0 dotnet test tests/CodeIntel.Cli.Tests/CodeIntel.Cli.Tests.csproj --no-restore -p:UseSharedCompilation=false -maxcpucount:1`
- Для analysis-тестов использовать:
  `env DOTNET_CLI_USE_MSBUILDNOINPROCNODE=1 DOTNET_BUILD_SERVER_KEEPALIVE=0 dotnet test tests/CodeIntel.Analysis.Tests/CodeIntel.Analysis.Tests.csproj --no-restore -p:UseSharedCompilation=false -maxcpucount:1`
- Если после изменения `*.csproj` тесты или сборка падают без диагностики, сначала выполнить `dotnet restore` для соответствующего тестового проекта, затем повторить `dotnet test`.
- В sandbox `dotnet test` может падать из-за ограничений на сокеты у test host; для достоверной проверки при необходимости запускать с эскалацией.
