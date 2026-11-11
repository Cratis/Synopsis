// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;
using Cratis.Synopsis.Rendering;

sealed record OutputPaths(string? Html, string? Json);

static class OutputWriter
{
    public static OutputPaths PathsFor(string output, string format)
    {
        var isHtml = HasExtension(output, ".html");
        var isJson = HasExtension(output, ".json");
        var stem = isHtml || isJson ? output[..^Path.GetExtension(output).Length] : Path.Combine(output, "synopsis");
        return format switch
        {
            "html" => new(isHtml ? output : $"{stem}.html", null),
            "json" => new(null, isJson ? output : $"{stem}.json"),
            "both" => new($"{stem}.html", $"{stem}.json"),
            _ => throw new CliError($"Unknown format '{format}'. Expected html, json, or both.")
        };
    }

    public static void WriteAll(OutputPaths paths, SynopsisDocument document)
    {
        if (paths.Html is not null)
        {
            Write(paths.Html, new HtmlRenderer().Render(document));
        }
        if (paths.Json is not null)
        {
            Write(paths.Json, new JsonRenderer().Render(document));
        }
    }

    static void Write(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    static bool HasExtension(string path, string extension) => Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase);
}
