// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;
using Cratis.Synopsis.Parsing;

namespace Cratis.Synopsis.Discovery;

/// <summary>
/// Discovers BDD specifications without building or executing the repository under analysis.
/// </summary>
public class SpecificationDiscoverer
{
    static readonly HashSet<string> DefaultExcluded = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".idea", ".vs", "bin", "obj", "node_modules", "Artifacts", "TestResults", "coverage"
    };

    readonly IReadOnlyList<ISpecificationParser> _parsers =
    [
        new CSharpSpecificationParser(),
        new TypeScriptSpecificationParser(),
        new ScreenplaySpecificationParser()
    ];

    /// <summary>
    /// Discovers and groups all supported specifications beneath the configured input.
    /// </summary>
    /// <param name="options">Discovery options.</param>
    /// <returns>A language-neutral behavior document.</returns>
    public SynopsisDocument Discover(DiscoveryOptions options)
    {
        var input = Path.GetFullPath(options.Input);
        if (!Directory.Exists(input))
        {
            throw new DirectoryNotFoundException($"The input directory '{input}' does not exist.");
        }

        var root = FindRepositoryRoot(input) ?? input;
        var context = new DiscoveryContext(root, options);
        var excluded = DefaultExcluded.Concat(options.Exclude).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var supported = _parsers.SelectMany(_ => _.Extensions).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files = Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories)
            .Where(path => supported.Contains(Path.GetExtension(path)))
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(excluded.Contains))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new SourceFile(
                path,
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                Path.GetRelativePath(input, path).Replace('\\', '/'),
                File.ReadAllText(path)))
            .ToList();

        var results = _parsers.Select(parser => parser.Parse(files.Where(file => parser.Extensions.Contains(Path.GetExtension(file.RelativePath))).ToList(), context)).ToList();
        var scenarios = results.SelectMany(_ => _.Scenarios).OrderBy(_ => _.Module, StringComparer.Ordinal).ThenBy(_ => _.Feature, StringComparer.Ordinal).ThenBy(_ => _.Title, StringComparer.Ordinal).ToList();
        var modules = scenarios.GroupBy(_ => _.Module, StringComparer.Ordinal).Select(module =>
            new BehaviorModule(module.Key, module.GroupBy(_ => _.Feature, StringComparer.Ordinal).Select(feature =>
                new BehaviorFeature(feature.Key, feature.ToList())).ToList())).ToList();
        var title = options.Title ?? $"{new DirectoryInfo(root).Name} Synopsis";
        return new("1.0", title, options.Description, root, options.SourceUrl, modules, results.SelectMany(_ => _.Diagnostics).ToList());
    }

    static string? FindRepositoryRoot(string path)
    {
        var current = new DirectoryInfo(path);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return null;
    }
}
