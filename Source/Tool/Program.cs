// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;
using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Rendering;

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintHelp();
    return 0;
}

if (args.Contains("--version"))
{
    Console.WriteLine(typeof(SpecificationDiscoverer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
    return 0;
}

try
{
    var arguments = Arguments.Parse(args);
    var input = Path.GetFullPath(arguments.Input);
    var fileConfiguration = LoadConfiguration(input, arguments.Configuration);
    var sourceUrl = arguments.SourceUrl ?? fileConfiguration?.SourceUrl ?? InferGitHubUrl(input);
    var options = new DiscoveryOptions
    {
        Input = input,
        Title = arguments.Title ?? fileConfiguration?.Title,
        Description = arguments.Description ?? fileConfiguration?.Description ?? "A living account of the behavior this system specifies.",
        SourceUrl = sourceUrl,
        SkipSegments = arguments.SkipSegments.Count > 0 ? arguments.SkipSegments : fileConfiguration?.SkipSegments ?? [],
        Exclude = (fileConfiguration?.Exclude ?? []).Concat(arguments.Exclude).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
    };

    var document = new SpecificationDiscoverer().Discover(options);
    var count = document.Scenarios.Count();
    if (count == 0 && arguments.FailOnEmpty)
    {
        Console.Error.WriteLine("No supported BDD specifications were found.");
        return 2;
    }

    var paths = OutputPaths(arguments.Output, arguments.Format);
    if (paths.Html is not null)
    {
        Write(paths.Html, new HtmlRenderer().Render(document));
    }
    if (paths.Json is not null)
    {
        Write(paths.Json, new JsonRenderer().Render(document));
    }

    if (!arguments.Quiet)
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

    if (arguments.Open && paths.Html is not null)
    {
        Process.Start(new ProcessStartInfo { FileName = Path.GetFullPath(paths.Html), UseShellExecute = true });
    }

    return 0;
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

static void Write(string path, string content)
{
    var fullPath = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    File.WriteAllText(fullPath, content);
}

static (string? Html, string? Json) OutputPaths(string output, string format)
{
    var extension = Path.GetExtension(output);
    var stem = extension.Equals(".html", StringComparison.OrdinalIgnoreCase) || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
        ? output[..^extension.Length]
        : Path.Combine(output, "synopsis");
    return format switch
    {
        "html" => (extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ? output : $"{stem}.html", null),
        "json" => (null, extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ? output : $"{stem}.json"),
        "both" => ($"{stem}.html", $"{stem}.json"),
        _ => throw new CliError($"Unknown format '{format}'. Expected html, json, or both.")
    };
}

static FileConfiguration? LoadConfiguration(string input, string? requested)
{
    var path = requested ?? FindUpward(input, "synopsis.json") ?? Path.Combine(input, "synopsis.json");
    if (!File.Exists(path))
    {
        return null;
    }

    return JsonSerializer.Deserialize<FileConfiguration>(File.ReadAllText(path), new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    });
}

static string? InferGitHubUrl(string input)
{
    var git = FindUpward(input, ".git");
    if (git is null || !Directory.Exists(git))
    {
        return null;
    }
    var configPath = Path.Combine(git, "config");

    var origin = File.ReadLines(configPath).SkipWhile(_ => !_.Trim().Equals("[remote \"origin\"]", StringComparison.Ordinal)).Skip(1).TakeWhile(_ => !_.TrimStart().StartsWith('[')).FirstOrDefault(_ => _.TrimStart().StartsWith("url =", StringComparison.Ordinal));
    if (origin is null)
    {
        return null;
    }

    var url = origin[(origin.IndexOf('=') + 1)..].Trim();
    if (url.StartsWith("git@github.com:", StringComparison.Ordinal))
    {
        url = $"https://github.com/{url[15..]}";
    }
    return url.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? url[..^4] : url;
}

static string? FindUpward(string input, string name)
{
    var current = new DirectoryInfo(input);
    while (current is not null)
    {
        var candidate = Path.Combine(current.FullName, name);
        if (File.Exists(candidate) || Directory.Exists(candidate))
        {
            return candidate;
        }
        current = current.Parent;
    }
    return null;
}

static void PrintHelp() => Console.WriteLine("""
Synopsis — turn executable examples into a living account of system behavior

Usage:
  synopsis [path] [options]

Options:
  -o, --output <path>       Output file or folder (default: synopsis.html)
  -f, --format <format>     html, json, or both (default: html)
      --title <text>        Document title
      --description <text>  Short introduction shown on the cover
      --source-url <url>    Repository URL used for source links
      --skip-segments <csv> Ignore path segments when inferring modules
      --exclude <csv>       Additional directory names to ignore
      --config <path>       Configuration file (default: <path>/synopsis.json)
      --fail-on-empty       Return exit code 2 when no specifications are found
      --open                Open the generated HTML in the default browser
      --quiet               Only print errors
  -h, --help                Show help
      --version             Show version

Supported inputs:
  C#          Cratis.Specifications / xUnit / NUnit: Establish, Because, [Fact], [Test]
  TypeScript  describe / it / test / beforeEach in .ts, .tsx, .js, and .jsx
  Screenplay  specification / given / when / then blocks in .play files

Examples:
  synopsis
  synopsis Source/Core --title "Ada — how it behaves" --open
  synopsis . --format both --output Artifacts/synopsis.html --fail-on-empty
""");

sealed record FileConfiguration
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? SourceUrl { get; init; }
    public IReadOnlyList<string> SkipSegments { get; init; } = [];
    public IReadOnlyList<string> Exclude { get; init; } = [];
}

sealed record Arguments(
    string Input,
    string Output,
    string Format,
    string? Title,
    string? Description,
    string? SourceUrl,
    string? Configuration,
    IReadOnlyList<string> SkipSegments,
    IReadOnlyList<string> Exclude,
    bool FailOnEmpty,
    bool Open,
    bool Quiet)
{
    public static Arguments Parse(string[] values)
    {
        var input = ".";
        var output = "synopsis.html";
        var format = "html";
        string? title = null;
        string? description = null;
        string? sourceUrl = null;
        string? configuration = null;
        var skip = new List<string>();
        var exclude = new List<string>();
        var failOnEmpty = false;
        var open = false;
        var quiet = false;
        var hasInput = false;
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            string Next() => ++index < values.Length ? values[index] : throw new CliError($"Missing value after '{value}'.");
            switch (value)
            {
                case "-o" or "--output": output = Next(); break;
                case "-f" or "--format": format = Next().ToLowerInvariant(); break;
                case "--title": title = Next(); break;
                case "--description": description = Next(); break;
                case "--source-url": sourceUrl = Next(); break;
                case "--config": configuration = Next(); break;
                case "--skip-segments": skip.AddRange(Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); break;
                case "--exclude": exclude.AddRange(Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); break;
                case "--fail-on-empty": failOnEmpty = true; break;
                case "--open": open = true; break;
                case "--quiet": quiet = true; break;
                default:
                    if (value.StartsWith('-')) throw new CliError($"Unknown option '{value}'.");
                    if (hasInput) throw new CliError("Only one input path can be supplied.");
                    input = value;
                    hasInput = true;
                    break;
            }
        }
        return new(input, output, format, title, description, sourceUrl, configuration, skip, exclude, failOnEmpty, open, quiet);
    }
}

sealed class CliError(string message) : Exception(message);
