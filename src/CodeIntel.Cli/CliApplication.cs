using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeIntel.Analysis;
using CodeIntel.Contracts;
using CodeIntel.Loader;

namespace CodeIntel.Cli;

/// <summary>
/// Выполняет командную оболочку и делегирует команды прикладным сервисам.
/// </summary>
public sealed class CliApplication(
    ISolutionSummaryLoader solutionSummaryLoader,
    IFindSymbolService findSymbolService,
    IFindReferencesService findReferencesService,
    IFindImplementationsService findImplementationsService,
    IAnalyzeImpactService analyzeImpactService)
{
    private const string SolutionSummaryCommand = "solution-summary";
    private const string FindSymbolCommand = "find-symbol";
    private const string FindReferencesCommand = "find-references";
    private const string FindImplementationsCommand = "find-implementations";
    private const string AnalyzeImpactCommand = "analyze-impact";
    private const string FindRegistrationsCommand = "find-registrations";
    private const string TraceCallersCommand = "trace-callers";
    private const string TracePropertyCallersCommand = "trace-property-callers";
    private const string SolutionOption = "--solution";
    private const string NameOption = "--name";
    private const string SymbolOption = "--symbol";
    private const string MethodOption = "--method";
    private const string PropertyOption = "--property";
    private const string AccessOption = "--access";
    private const string DepthOption = "--depth";
    private const string IncludeTestsOption = "--include-tests";
    private const string SolutionSummaryUsageText =
        "Usage: codeintel solution-summary --solution <path-to-solution.sln|path-to-solution.slnx>";
    private const string FindSymbolUsageText =
        "Usage: codeintel find-symbol --solution <path-to-solution.sln|path-to-solution.slnx> --name <symbol-name>";
    private const string FindReferencesUsageText =
        "Usage: codeintel find-references --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name>";
    private const string FindImplementationsUsageText =
        "Usage: codeintel find-implementations --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name> [--include-tests]";
    private const string AnalyzeImpactUsageText =
        "Usage: codeintel analyze-impact --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name> [--include-tests]";
    private const string FindRegistrationsUsageText =
        "Usage: codeintel find-registrations --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name>";
    private const string TraceCallersUsageText =
        "Usage: codeintel trace-callers --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name> --method <method-name> [--depth <n>] [--include-tests]";
    private const string TracePropertyCallersUsageText =
        "Usage: codeintel trace-property-callers --solution <path-to-solution.sln|path-to-solution.slnx> --symbol <type-name> --property <property-name> [--access <get|set|both>] [--depth <n>] [--include-tests]";
    private static readonly string GeneralUsageText =
        $"{SolutionSummaryUsageText}{Environment.NewLine}{FindSymbolUsageText}{Environment.NewLine}{FindReferencesUsageText}{Environment.NewLine}{FindImplementationsUsageText}{Environment.NewLine}{AnalyzeImpactUsageText}{Environment.NewLine}{FindRegistrationsUsageText}{Environment.NewLine}{TraceCallersUsageText}{Environment.NewLine}{TracePropertyCallersUsageText}";

    private readonly IFindRegistrationsService findRegistrationsService = new FindRegistrationsService();
    private readonly ITraceCallersService traceCallersService = new TraceCallersService();
    private readonly ITracePropertyCallersService tracePropertyCallersService = new TracePropertyCallersService();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    static CliApplication()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    /// <summary>
    /// Инициализирует CLI-оболочку с явной передачей сервиса поиска регистраций.
    /// </summary>
    public CliApplication(
        ISolutionSummaryLoader solutionSummaryLoader,
        IFindSymbolService findSymbolService,
        IFindReferencesService findReferencesService,
        IFindImplementationsService findImplementationsService,
        IAnalyzeImpactService analyzeImpactService,
        IFindRegistrationsService findRegistrationsService)
        : this(solutionSummaryLoader, findSymbolService, findReferencesService, findImplementationsService, analyzeImpactService)
    {
        this.findRegistrationsService = findRegistrationsService;
    }

    /// <summary>
    /// Инициализирует CLI-оболочку с явной передачей сервиса трассировки вызовов.
    /// </summary>
    public CliApplication(
        ISolutionSummaryLoader solutionSummaryLoader,
        IFindSymbolService findSymbolService,
        IFindReferencesService findReferencesService,
        IFindImplementationsService findImplementationsService,
        IAnalyzeImpactService analyzeImpactService,
        IFindRegistrationsService findRegistrationsService,
        ITraceCallersService traceCallersService)
        : this(solutionSummaryLoader, findSymbolService, findReferencesService, findImplementationsService, analyzeImpactService, findRegistrationsService)
    {
        this.traceCallersService = traceCallersService;
    }

    /// <summary>
    /// Инициализирует CLI-оболочку с явной передачей сервисов трассировки вызовов методов и свойств.
    /// </summary>
    public CliApplication(
        ISolutionSummaryLoader solutionSummaryLoader,
        IFindSymbolService findSymbolService,
        IFindReferencesService findReferencesService,
        IFindImplementationsService findImplementationsService,
        IAnalyzeImpactService analyzeImpactService,
        IFindRegistrationsService findRegistrationsService,
        ITraceCallersService traceCallersService,
        ITracePropertyCallersService tracePropertyCallersService)
        : this(solutionSummaryLoader, findSymbolService, findReferencesService, findImplementationsService, analyzeImpactService, findRegistrationsService, traceCallersService)
    {
        this.tracePropertyCallersService = tracePropertyCallersService;
    }

    /// <summary>
    /// Запускает CLI с заданными аргументами.
    /// </summary>
    public async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0)
        {
            await error.WriteLineAsync(SolutionSummaryUsageText);
            return 1;
        }

        var command = args[0];
        var remainingArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();

        if (string.Equals(command, SolutionSummaryCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunSolutionSummaryCommandAsync(remainingArgs, output, error, cancellationToken);
        }

        if (string.Equals(command, FindSymbolCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunFindSymbolCommandAsync(remainingArgs, output, error, cancellationToken);
        }

        if (string.Equals(command, FindReferencesCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunFindReferencesCommandAsync(remainingArgs, output, error, cancellationToken);
        }

        if (string.Equals(command, FindImplementationsCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunFindImplementationsCommandAsync(remainingArgs, output, error, cancellationToken);
        }

        if (string.Equals(command, AnalyzeImpactCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunAnalyzeImpactCommandAsync(remainingArgs, output, error, cancellationToken);
        }

        if (string.Equals(command, FindRegistrationsCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunFindRegistrationsCommandAsync(remainingArgs, output, error, cancellationToken);
        }

        if (string.Equals(command, TraceCallersCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunTraceCallersCommandAsync(remainingArgs, output, error, cancellationToken);
        }

        if (string.Equals(command, TracePropertyCallersCommand, StringComparison.OrdinalIgnoreCase))
        {
            return await RunTracePropertyCallersCommandAsync(remainingArgs, output, error, cancellationToken);
        }

        await error.WriteLineAsync(GeneralUsageText);
        return 1;
    }

    private async Task<int> RunFindRegistrationsCommandAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryParseFindRegistrationsArguments(args, out var solutionPath, out var symbol, out var errorMessage))
        {
            await error.WriteLineAsync(errorMessage);
            return 1;
        }

        try
        {
            var response = await findRegistrationsService.FindAsync(solutionPath, symbol, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
            return 0;
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private async Task<int> RunAnalyzeImpactCommandAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryParseAnalyzeImpactArguments(args, out var solutionPath, out var symbol, out var includeTests, out var errorMessage))
        {
            await error.WriteLineAsync(errorMessage);
            return 1;
        }

        try
        {
            var response = await analyzeImpactService.AnalyzeAsync(solutionPath, symbol, includeTests, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
            return 0;
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private async Task<int> RunFindImplementationsCommandAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryParseFindImplementationsArguments(args, out var solutionPath, out var symbol, out var includeTests, out var errorMessage))
        {
            await error.WriteLineAsync(errorMessage);
            return 1;
        }

        try
        {
            var response = await findImplementationsService.FindAsync(solutionPath, symbol, includeTests, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
            return 0;
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private async Task<int> RunFindReferencesCommandAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryParseFindReferencesArguments(args, out var solutionPath, out var symbol, out var errorMessage))
        {
            await error.WriteLineAsync(errorMessage);
            return 1;
        }

        try
        {
            var response = await findReferencesService.FindAsync(solutionPath, symbol, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
            return 0;
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private async Task<int> RunSolutionSummaryCommandAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryParseSolutionSummaryArguments(args, out var solutionPath, out var errorMessage))
        {
            await error.WriteLineAsync(errorMessage);
            return 1;
        }

        try
        {
            var summary = await solutionSummaryLoader.LoadAsync(solutionPath, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(summary, JsonOptions));
            return 0;
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private async Task<int> RunFindSymbolCommandAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryParseFindSymbolArguments(args, out var solutionPath, out var name, out var errorMessage))
        {
            await error.WriteLineAsync(errorMessage);
            return 1;
        }

        try
        {
            var response = await findSymbolService.FindAsync(solutionPath, name, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
            return 0;
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private static bool TryParseSolutionSummaryArguments(
        IReadOnlyList<string> args,
        out string solutionPath,
        out string errorMessage)
    {
        solutionPath = string.Empty;
        errorMessage = SolutionSummaryUsageText;

        if (args.Count == 0)
        {
            errorMessage = "Missing required solution path.";
            return false;
        }

        var index = 0;
        while (index < args.Count)
        {
            var current = args[index];

            if (string.Equals(current, SolutionOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --solution.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(solutionPath))
                {
                    errorMessage = "The --solution option can only be specified once.";
                    return false;
                }

                solutionPath = args[index];
                index++;
                continue;
            }

            if (current.StartsWith("--", StringComparison.Ordinal))
            {
                errorMessage = $"Unknown option: {current}";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(solutionPath))
            {
                errorMessage = "Solution path can only be specified once.";
                return false;
            }

            solutionPath = current;
            index++;
        }

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            errorMessage = "Missing required solution path.";
            return false;
        }

        return true;
    }

    private static bool TryParseFindSymbolArguments(
        IReadOnlyList<string> args,
        out string solutionPath,
        out string name,
        out string errorMessage)
    {
        solutionPath = string.Empty;
        name = string.Empty;
        errorMessage = FindSymbolUsageText;

        var index = 0;
        while (index < args.Count)
        {
            var current = args[index];

            if (string.Equals(current, SolutionOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --solution.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(solutionPath))
                {
                    errorMessage = "The --solution option can only be specified once.";
                    return false;
                }

                solutionPath = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, NameOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --name.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    errorMessage = "The --name option can only be specified once.";
                    return false;
                }

                name = args[index];
                index++;
                continue;
            }

            if (current.StartsWith("--", StringComparison.Ordinal))
            {
                errorMessage = $"Unknown option: {current}";
                return false;
            }

            errorMessage = $"Unknown argument: {current}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            errorMessage = "Missing required --solution option.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errorMessage = "Missing required --name option.";
            return false;
        }

        return true;
    }

    private static bool TryParseFindReferencesArguments(
        IReadOnlyList<string> args,
        out string solutionPath,
        out string symbol,
        out string errorMessage)
    {
        solutionPath = string.Empty;
        symbol = string.Empty;
        errorMessage = FindReferencesUsageText;

        var index = 0;
        while (index < args.Count)
        {
            var current = args[index];

            if (string.Equals(current, SolutionOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --solution.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(solutionPath))
                {
                    errorMessage = "The --solution option can only be specified once.";
                    return false;
                }

                solutionPath = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, SymbolOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --symbol.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    errorMessage = "The --symbol option can only be specified once.";
                    return false;
                }

                symbol = args[index];
                index++;
                continue;
            }

            errorMessage = $"Unknown option: {current}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            errorMessage = "Missing required --solution option.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            errorMessage = "Missing required --symbol option.";
            return false;
        }

        return true;
    }

    private static bool TryParseFindImplementationsArguments(
        IReadOnlyList<string> args,
        out string solutionPath,
        out string symbol,
        out bool includeTests,
        out string errorMessage)
    {
        return TryParseSymbolCommandArguments(
            args,
            FindImplementationsUsageText,
            out solutionPath,
            out symbol,
            out includeTests,
            out errorMessage);
    }

    private static bool TryParseAnalyzeImpactArguments(
        IReadOnlyList<string> args,
        out string solutionPath,
        out string symbol,
        out bool includeTests,
        out string errorMessage)
    {
        return TryParseSymbolCommandArguments(
            args,
            AnalyzeImpactUsageText,
            out solutionPath,
            out symbol,
            out includeTests,
            out errorMessage);
    }

    private static bool TryParseFindRegistrationsArguments(
        IReadOnlyList<string> args,
        out string solutionPath,
        out string symbol,
        out string errorMessage)
    {
        return TryParseSolutionAndSymbolArguments(
            args,
            FindRegistrationsUsageText,
            out solutionPath,
            out symbol,
            out errorMessage);
    }

    private static bool TryParseSymbolCommandArguments(
        IReadOnlyList<string> args,
        string usageText,
        out string solutionPath,
        out string symbol,
        out bool includeTests,
        out string errorMessage)
    {
        solutionPath = string.Empty;
        symbol = string.Empty;
        includeTests = false;
        errorMessage = usageText;

        var index = 0;
        while (index < args.Count)
        {
            var current = args[index];

            if (string.Equals(current, SolutionOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --solution.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(solutionPath))
                {
                    errorMessage = "The --solution option can only be specified once.";
                    return false;
                }

                solutionPath = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, SymbolOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --symbol.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    errorMessage = "The --symbol option can only be specified once.";
                    return false;
                }

                symbol = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, IncludeTestsOption, StringComparison.OrdinalIgnoreCase))
            {
                if (includeTests)
                {
                    errorMessage = "The --include-tests option can only be specified once.";
                    return false;
                }

                includeTests = true;
                index++;
                continue;
            }

            errorMessage = $"Unknown option: {current}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            errorMessage = "Missing required --solution option.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            errorMessage = "Missing required --symbol option.";
            return false;
        }

        return true;
    }

    private async Task<int> RunTraceCallersCommandAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryParseTraceCallersArguments(args, out var solutionPath, out var symbol, out var method, out var maxDepth, out var includeTests, out var errorMessage))
        {
            await error.WriteLineAsync(errorMessage);
            return 1;
        }

        try
        {
            var response = await traceCallersService.FindAsync(solutionPath, symbol, method, maxDepth, includeTests, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
            return 0;
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private static bool TryParseTraceCallersArguments(
        IReadOnlyList<string> args,
        out string solutionPath,
        out string symbol,
        out string method,
        out int maxDepth,
        out bool includeTests,
        out string errorMessage)
    {
        solutionPath = string.Empty;
        symbol = string.Empty;
        method = string.Empty;
        maxDepth = 15;
        includeTests = false;
        errorMessage = TraceCallersUsageText;

        var index = 0;
        while (index < args.Count)
        {
            var current = args[index];

            if (string.Equals(current, SolutionOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --solution.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(solutionPath))
                {
                    errorMessage = "The --solution option can only be specified once.";
                    return false;
                }

                solutionPath = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, SymbolOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --symbol.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    errorMessage = "The --symbol option can only be specified once.";
                    return false;
                }

                symbol = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, MethodOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --method.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(method))
                {
                    errorMessage = "The --method option can only be specified once.";
                    return false;
                }

                method = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, DepthOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --depth.";
                    return false;
                }

                if (!int.TryParse(args[index], out var parsedDepth) || parsedDepth < 1)
                {
                    errorMessage = "The --depth option must be a positive integer.";
                    return false;
                }

                maxDepth = parsedDepth;
                index++;
                continue;
            }

            if (string.Equals(current, IncludeTestsOption, StringComparison.OrdinalIgnoreCase))
            {
                if (includeTests)
                {
                    errorMessage = "The --include-tests option can only be specified once.";
                    return false;
                }

                includeTests = true;
                index++;
                continue;
            }

            errorMessage = $"Unknown option: {current}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            errorMessage = "Missing required --solution option.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            errorMessage = "Missing required --symbol option.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(method))
        {
            errorMessage = "Missing required --method option.";
            return false;
        }

        return true;
    }

    private async Task<int> RunTracePropertyCallersCommandAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!TryParseTracePropertyCallersArguments(
                args,
                out var solutionPath,
                out var symbol,
                out var property,
                out var access,
                out var maxDepth,
                out var includeTests,
                out var errorMessage))
        {
            await error.WriteLineAsync(errorMessage);
            return 1;
        }

        try
        {
            var response = await tracePropertyCallersService.FindAsync(solutionPath, symbol, property, access, maxDepth, includeTests, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
            return 0;
        }
        catch (Exception exception) when (exception is FileNotFoundException or NotSupportedException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private static bool TryParseTracePropertyCallersArguments(
        IReadOnlyList<string> args,
        out string solutionPath,
        out string symbol,
        out string property,
        out PropertyAccessKindDto access,
        out int maxDepth,
        out bool includeTests,
        out string errorMessage)
    {
        solutionPath = string.Empty;
        symbol = string.Empty;
        property = string.Empty;
        access = PropertyAccessKindDto.Both;
        maxDepth = 15;
        includeTests = false;
        errorMessage = TracePropertyCallersUsageText;

        var index = 0;
        while (index < args.Count)
        {
            var current = args[index];

            if (string.Equals(current, SolutionOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --solution.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(solutionPath))
                {
                    errorMessage = "The --solution option can only be specified once.";
                    return false;
                }

                solutionPath = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, SymbolOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --symbol.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    errorMessage = "The --symbol option can only be specified once.";
                    return false;
                }

                symbol = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, PropertyOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --property.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(property))
                {
                    errorMessage = "The --property option can only be specified once.";
                    return false;
                }

                property = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, AccessOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --access.";
                    return false;
                }

                if (!TryParsePropertyAccess(args[index], out access))
                {
                    errorMessage = "The --access option must be one of: get, set, both.";
                    return false;
                }

                index++;
                continue;
            }

            if (string.Equals(current, DepthOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --depth.";
                    return false;
                }

                if (!int.TryParse(args[index], out var parsedDepth) || parsedDepth < 1)
                {
                    errorMessage = "The --depth option must be a positive integer.";
                    return false;
                }

                maxDepth = parsedDepth;
                index++;
                continue;
            }

            if (string.Equals(current, IncludeTestsOption, StringComparison.OrdinalIgnoreCase))
            {
                if (includeTests)
                {
                    errorMessage = "The --include-tests option can only be specified once.";
                    return false;
                }

                includeTests = true;
                index++;
                continue;
            }

            errorMessage = $"Unknown option: {current}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            errorMessage = "Missing required --solution option.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            errorMessage = "Missing required --symbol option.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(property))
        {
            errorMessage = "Missing required --property option.";
            return false;
        }

        return true;
    }

    private static bool TryParsePropertyAccess(string value, out PropertyAccessKindDto access)
    {
        if (string.Equals(value, "get", StringComparison.OrdinalIgnoreCase))
        {
            access = PropertyAccessKindDto.Get;
            return true;
        }

        if (string.Equals(value, "set", StringComparison.OrdinalIgnoreCase))
        {
            access = PropertyAccessKindDto.Set;
            return true;
        }

        if (string.Equals(value, "both", StringComparison.OrdinalIgnoreCase))
        {
            access = PropertyAccessKindDto.Both;
            return true;
        }

        access = PropertyAccessKindDto.Both;
        return false;
    }

    private static bool TryParseSolutionAndSymbolArguments(
        IReadOnlyList<string> args,
        string usageText,
        out string solutionPath,
        out string symbol,
        out string errorMessage)
    {
        solutionPath = string.Empty;
        symbol = string.Empty;
        errorMessage = usageText;

        var index = 0;
        while (index < args.Count)
        {
            var current = args[index];

            if (string.Equals(current, SolutionOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --solution.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(solutionPath))
                {
                    errorMessage = "The --solution option can only be specified once.";
                    return false;
                }

                solutionPath = args[index];
                index++;
                continue;
            }

            if (string.Equals(current, SymbolOption, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    errorMessage = "Missing value for --symbol.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    errorMessage = "The --symbol option can only be specified once.";
                    return false;
                }

                symbol = args[index];
                index++;
                continue;
            }

            errorMessage = $"Unknown option: {current}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            errorMessage = "Missing required --solution option.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            errorMessage = "Missing required --symbol option.";
            return false;
        }

        return true;
    }
}
