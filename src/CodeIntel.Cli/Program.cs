using CodeIntel.Analysis;
using CodeIntel.Cli;
using CodeIntel.Loader;

var application = new CliApplication(
    new SolutionSummaryLoader(),
    new FindSymbolService(),
    new FindReferencesService(),
    new FindImplementationsService(),
    new AnalyzeImpactService(
        new FindReferencesService(),
        new FindImplementationsService()));
return await application.RunAsync(args, Console.Out, Console.Error);
