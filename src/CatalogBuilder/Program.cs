using CatalogBuilder.Pipeline;
using CatalogBuilder.Sources;

// Usage (run inside the catalog repo):
//   dotnet run --project src/CatalogBuilder -- build [--no-marketplace-ids] [--lenient] [--languages en,de]
//   dotnet run --project src/CatalogBuilder -- package [--version 2026.10.05.1]
//   dotnet run --project src/CatalogBuilder -- validate

var command = args.FirstOrDefault() ?? "help";
string? Option(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var config = PipelineConfig.Load(PipelineConfig.FindRepoRoot());

switch (command)
{
    case "build":
    {
        using var http = new Http();
        var build = new CatalogBuild(config, new TcgdexClient(http), new PokeApiClient(http), Console.Out);
        var languages = Option("--languages")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return await build.RunAsync(new CatalogBuildOptions(!args.Contains("--no-marketplace-ids"), args.Contains("--lenient"), languages), cts.Token);
    }
    case "package":
        return new Packager(config, Console.Out).Run(Option("--version") ?? Packager.DefaultVersion());
    case "validate":
    {
        var errors = new SchemaValidator(config.SchemaDir).ValidateDirectory(config.CatalogDir);
        foreach (var error in errors) Console.WriteLine(error);
        Console.WriteLine(errors.Count == 0 ? "catalog/ is valid." : $"{errors.Count} error(s).");
        return errors.Count == 0 ? 0 : 2;
    }
    default:
        Console.WriteLine("Commands: build [--no-marketplace-ids] [--lenient] [--languages en,de] | package [--version X] | validate");
        return command == "help" ? 0 : 1;
}
