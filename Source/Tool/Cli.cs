// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Model;

static class Cli
{
    public static int Run(string[] args)
    {
        try
        {
            return Execute(Arguments.Parse(args));
        }
        catch (CliError error)
        {
            Console.Error.WriteLine($"synopsis: {error.Message}");
            Console.Error.WriteLine("Run 'synopsis --help' for usage.");
            return 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"synopsis: {error.Message}");
            return 1;
        }
    }

    static int Execute(Arguments arguments)
    {
        var document = new SpecificationDiscoverer().Discover(OptionsFrom(arguments));
        var count = document.Scenarios.Count();
        if (count == 0 && arguments.FailOnEmpty)
        {
            Console.Error.WriteLine("No supported BDD specifications were found.");
            return 2;
        }

        var paths = OutputWriter.PathsFor(arguments.Output, arguments.Format);
        OutputWriter.WriteAll(paths, document);
        if (!arguments.Quiet)
        {
            PrintSummary(document, count, paths);
        }
        if (arguments.Open && paths.Html is not null)
        {
            Process.Start(new ProcessStartInfo { FileName = Path.GetFullPath(paths.Html), UseShellExecute = true });
        }
        return 0;
    }

    static DiscoveryOptions OptionsFrom(Arguments arguments)
    {
        var input = Path.GetFullPath(arguments.Input);
        var fileConfiguration = ProjectConfiguration.Load(input, arguments.Configuration);
        return new DiscoveryOptions
        {
            Input = input,
            Title = arguments.Title ?? fileConfiguration?.Title,
            Description = arguments.Description ?? fileConfiguration?.Description ?? "A living account of the behavior this system specifies.",
            SourceUrl = arguments.SourceUrl ?? fileConfiguration?.SourceUrl ?? ProjectConfiguration.InferGitHubUrl(input),
            SkipSegments = SkipSegmentsOf(arguments, fileConfiguration),
            Exclude = ExcludeOf(arguments, fileConfiguration)
        };
    }

    static IReadOnlyList<string> SkipSegmentsOf(Arguments arguments, FileConfiguration? configuration) =>
        arguments.SkipSegments.Count > 0 ? arguments.SkipSegments : configuration?.SkipSegments ?? [];

    static List<string> ExcludeOf(Arguments arguments, FileConfiguration? configuration) =>
        (configuration?.Exclude ?? []).Concat(arguments.Exclude).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    static void PrintSummary(SynopsisDocument document, int count, OutputPaths paths)
    {
        Console.WriteLine($"Synopsis found {count} scenarios with {document.Scenarios.Sum(_ => _.Then.Count)} outcomes across {document.Modules.Count} modules.");
        if (paths.Html is not null)
        {
            Console.WriteLine($"  HTML  {Path.GetFullPath(paths.Html)}");
        }
        if (paths.Json is not null)
        {
            Console.WriteLine($"  JSON  {Path.GetFullPath(paths.Json)}");
        }
        if (document.Diagnostics.Count > 0)
        {
            Console.WriteLine($"  Notes {document.Diagnostics.Count} non-fatal discovery diagnostics (included in the output)");
        }
    }
}
