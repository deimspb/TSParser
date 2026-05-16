using CorpusHarvester;

return CliRunner.Run(args);

internal static class CliRunner
{
    private const string DefaultTsRoot = @"D:\Dvb\dvb_lib";
    private const string DefaultStaging = @"D:\Dvb\ts_harvest\descriptors";

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var options = ParseOptions(args.Skip(1).ToArray());

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        if (!Directory.Exists(Path.Combine(repoRoot, "TSParser")))
        {
            repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory()));
        }

        var fixturesRoot = options.FixturesRoot
            ?? Environment.GetEnvironmentVariable("TSPARSER_TEST_FIXTURES")
            ?? Path.Combine(repoRoot, "TSParser.Tests", "TestResources");

        return command switch
        {
            "harvest" => RunHarvest(options),
            "select" => RunSelect(options, fixturesRoot),
            _ => UnknownCommand(command),
        };
    }

    private static int RunHarvest(Options options)
    {
        var tsRoot = options.TsRoot
            ?? Environment.GetEnvironmentVariable("TSPARSER_TS_ROOT")
            ?? DefaultTsRoot;
        var staging = options.StagingRoot
            ?? Environment.GetEnvironmentVariable("TSPARSER_DESCRIPTOR_STAGING")
            ?? DefaultStaging;

        if (!Directory.Exists(tsRoot))
        {
            Console.Error.WriteLine($"TS corpus directory not found: {tsRoot}");
            return 1;
        }

        var tsFiles = Directory.GetFiles(tsRoot, "*.ts", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tsFiles.Length == 0)
        {
            Console.WriteLine($"No .ts files under {tsRoot}");
            return 0;
        }

        Console.WriteLine($"TS root:    {tsRoot}");
        Console.WriteLine($"Staging:    {staging}");
        Console.WriteLine($"Run time:   {options.RunTimeMs} ms");
        Console.WriteLine($"TS files:   {tsFiles.Length}");
        Console.WriteLine();

        var collector = new DescriptorCollector(staging);
        var harvester = new TsCorpusHarvester(collector, options.RunTimeMs);

        foreach (var ts in tsFiles)
        {
            try
            {
                harvester.HarvestFile(ts);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  !! Failed: {Path.GetFileName(ts)}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Harvest complete.");
        Console.WriteLine($"  Unique keys: {collector.UniqueCount}");
        Console.WriteLine($"  Files saved: {collector.SavedCount}");
        Console.WriteLine($"  Staging:     {staging}");
        return 0;
    }

    private static int RunSelect(Options options, string fixturesRoot)
    {
        var staging = options.StagingRoot
            ?? Environment.GetEnvironmentVariable("TSPARSER_DESCRIPTOR_STAGING")
            ?? DefaultStaging;

        Console.WriteLine($"Staging:    {staging}");
        Console.WriteLine($"Fixtures:   {fixturesRoot}");
        Console.WriteLine();

        try
        {
            var selector = new DescriptorSampleSelector(staging, fixturesRoot, options.TargetSamples);
            var copied = selector.SelectAll(options.DryRun);
            Console.WriteLine();
            Console.WriteLine(options.DryRun ? "Dry-run complete." : $"Selection complete. Copied {copied} descriptor fixture(s).");
            return 0;
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Run 'harvest' first or set TSPARSER_DESCRIPTOR_STAGING.");
            return 1;
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            CorpusHarvester — collect unique DVB/AIT/SCTE descriptors from TS files and select test fixtures.

            Usage:
              CorpusHarvester harvest [options]
              CorpusHarvester select [options]

            Commands:
              harvest   Parse .ts files with TsParser (table mode), save unique .desc to staging.
              select    Pick S/M1/M2/L samples per descriptor group into TestResources/Descriptors.

            Options:
              --ts-root <path>       TS corpus (env TSPARSER_TS_ROOT, default D:\Dvb\dvb_lib)
              --staging <path>       Staging output (env TSPARSER_DESCRIPTOR_STAGING)
              --fixtures <path>      TestResources root (env TSPARSER_TEST_FIXTURES)
              --run-time <ms>        Parser run time per file (default 60000)
              --target-samples <n>   Samples per group for select (default 4)
              --dry-run              select only: print actions without writing

            Examples:
              dotnet run --project tools/CorpusHarvester -- harvest
              dotnet run --project tools/CorpusHarvester -- select
              CorpusHarvester harvest --ts-root D:\Dvb\dvb_lib --staging D:\Dvb\ts_harvest\descriptors
            """);
    }

    private static Options ParseOptions(string[] args)
    {
        var options = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--ts-root" when i + 1 < args.Length:
                    options.TsRoot = args[++i];
                    break;
                case "--staging" when i + 1 < args.Length:
                    options.StagingRoot = args[++i];
                    break;
                case "--fixtures" when i + 1 < args.Length:
                    options.FixturesRoot = args[++i];
                    break;
                case "--run-time" when i + 1 < args.Length && int.TryParse(args[++i], out var runMs):
                    options.RunTimeMs = runMs;
                    break;
                case "--target-samples" when i + 1 < args.Length && int.TryParse(args[++i], out var target):
                    options.TargetSamples = target;
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
            }
        }

        return options;
    }

    private sealed class Options
    {
        public string? TsRoot { get; set; }
        public string? StagingRoot { get; set; }
        public string? FixturesRoot { get; set; }
        public int RunTimeMs { get; set; } = 60_000;
        public int TargetSamples { get; set; } = 4;
        public bool DryRun { get; set; }
    }
}
