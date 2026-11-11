// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

static class Help
{
    public static void Print() => Console.WriteLine("""
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
}
