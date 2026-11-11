// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

sealed record FileConfiguration
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? SourceUrl { get; init; }
    public IReadOnlyList<string> SkipSegments { get; init; } = [];
    public IReadOnlyList<string> Exclude { get; init; } = [];
}

static class ProjectConfiguration
{
    public static FileConfiguration? Load(string input, string? requested)
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

    public static string? InferGitHubUrl(string input)
    {
        var git = FindUpward(input, ".git");
        if (git is null || !Directory.Exists(git))
        {
            return null;
        }

        var origin = OriginUrlLine(Path.Combine(git, "config"));
        if (origin is null)
        {
            return null;
        }

        var url = origin[(origin.IndexOf('=') + 1)..].Trim();
        return RepositoryUrl.FromRemote(url);
    }

    static string? OriginUrlLine(string configPath) => File.ReadLines(configPath)
        .SkipWhile(_ => !_.Trim().Equals("[remote \"origin\"]", StringComparison.Ordinal))
        .Skip(1)
        .TakeWhile(_ => !_.TrimStart().StartsWith('['))
        .FirstOrDefault(_ => _.TrimStart().StartsWith("url =", StringComparison.Ordinal));

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
}
