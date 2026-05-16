using System.Text.Json;

namespace BlessManifest;

internal static class Program
{
    private static int Main(string[] args)
    {
        var fixturesRoot = GetArg(args, "--fixtures-root")
            ?? Environment.GetEnvironmentVariable("TSPARSER_TEST_FIXTURES");
        var tablesOnly = args.Contains("--tables-only", StringComparer.OrdinalIgnoreCase);
        var descriptorsOnly = args.Contains("--descriptors-only", StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(fixturesRoot))
        {
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            fixturesRoot = Path.Combine(repoRoot, "TSParser.Tests", "TestResources");
        }

        fixturesRoot = Path.GetFullPath(fixturesRoot);
        Directory.CreateDirectory(fixturesRoot);

        var blessTables = !descriptorsOnly;
        var blessDescriptors = !tablesOnly;

        try
        {
            if (blessTables)
            {
                var tablesPath = Path.Combine(fixturesRoot, "manifest.tables.json");
                var existingTables = LoadManifest<TablesManifest>(tablesPath);
                var tablesManifest = TableBlesser.Bless(fixturesRoot, existingTables);
                if (existingTables?.GeneratedAt is { } generatedAt)
                {
                    tablesManifest.GeneratedAt = generatedAt;
                }

                if (existingTables?.StagingRoot is { } stagingRoot)
                {
                    tablesManifest.StagingRoot = stagingRoot;
                }

                File.WriteAllText(tablesPath, JsonSerializer.Serialize(tablesManifest, ManifestJson.Options));
                Console.WriteLine($"Tables manifest: {tablesPath} ({tablesManifest.Tables.Count} fixture(s))");
            }

            if (blessDescriptors)
            {
                var descriptorsPath = Path.Combine(fixturesRoot, "manifest.descriptors.json");
                var existingDescriptors = LoadManifest<DescriptorsManifest>(descriptorsPath);
                var descriptorsManifest = DescriptorBlesser.Bless(fixturesRoot, existingDescriptors);
                if (existingDescriptors?.GeneratedAt is { } generatedAt)
                {
                    descriptorsManifest.GeneratedAt = generatedAt;
                }

                if (existingDescriptors?.StagingRoot is { } stagingRoot)
                {
                    descriptorsManifest.StagingRoot = stagingRoot;
                }

                File.WriteAllText(descriptorsPath, JsonSerializer.Serialize(descriptorsManifest, ManifestJson.Options));
                Console.WriteLine($"Descriptors manifest: {descriptorsPath} ({descriptorsManifest.Descriptors.Count} fixture(s))");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static T? LoadManifest<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, ManifestJson.Options);
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return args[i][(name.Length + 1)..];
            }
        }

        return null;
    }
}
