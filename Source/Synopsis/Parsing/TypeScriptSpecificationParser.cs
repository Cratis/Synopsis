// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Parsing;

internal partial class TypeScriptSpecificationParser : ISpecificationParser
{
    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ts", ".tsx", ".js", ".jsx" };

    public ParseResult Parse(IReadOnlyList<SourceFile> files, DiscoveryContext context)
    {
        var scenarios = new List<BehaviorScenario>();
        var diagnostics = new List<DiscoveryDiagnostic>();
        foreach (var file in files)
        {
            var describes = Describe().Matches(file.Content).Select(match => CreateBlock(file.Content, match)).Where(_ => _ is not null).Cast<Block>().ToList();
            var tests = Test().Matches(file.Content).ToList();
            foreach (var describe in describes)
            {
                var owned = tests.Where(test => test.Index > describe.Start && test.Index < describe.End)
                    .Where(test => describes.Where(other => other != describe && test.Index > other.Start && test.Index < other.End).All(other => other.End - other.Start >= describe.End - describe.Start))
                    .ToList();
                if (owned.Count == 0)
                {
                    continue;
                }

                var classification = context.Classify(file.RelativePath);
                var line = LineAt(file.Content, describe.Start);
                var beforeEach = BeforeEach().Match(file.Content, describe.Start, describe.End - describe.Start);
                var given = beforeEach.Success
                    ? new[] { new BehaviorStep("The scenario context", ExtractArrowBody(file.Content, beforeEach.Index, describe.End)) }
                    : [];
                var outcomes = owned.Select(test => new BehaviorStep(test.Groups[2].Value, ExtractArrowBody(file.Content, test.Index, describe.End))).ToList();
                var title = describe.Title;
                scenarios.Add(new(
                    $"ts:{file.RelativePath}:{line}",
                    classification.Module,
                    classification.Feature,
                    classification.Subject,
                    Humanizer.Identifier(title, "when "),
                    given,
                    new(Humanizer.Identifier(title, "when ")),
                    outcomes,
                    file.RelativePath.EndsWith("x", StringComparison.OrdinalIgnoreCase) ? "TSX" : "TypeScript",
                    "Frontend",
                    context.Locate(file.RelativePath, line)));
            }
        }

        return new(scenarios, diagnostics);
    }

    static Block? CreateBlock(string source, Match match)
    {
        var open = source.IndexOf('{', match.Index + match.Length - 1);
        if (open < 0)
        {
            return null;
        }

        var close = MatchingBrace(source, open);
        return close < 0 ? null : new(match.Groups[2].Value, open, close);
    }

    static int MatchingBrace(string source, int open)
    {
        var depth = 0;
        var quote = '\0';
        var escaped = false;
        var lineComment = false;
        var blockComment = false;
        for (var index = open; index < source.Length; index++)
        {
            var character = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (lineComment)
            {
                lineComment = character != '\n';
                continue;
            }

            if (blockComment)
            {
                if (character == '*' && next == '/')
                {
                    blockComment = false;
                    index++;
                }
                continue;
            }

            if (quote != '\0')
            {
                if (!escaped && character == quote)
                {
                    quote = '\0';
                }
                escaped = !escaped && character == '\\';
                if (character != '\\')
                {
                    escaped = false;
                }
                continue;
            }

            if (character == '/' && next == '/')
            {
                lineComment = true;
                index++;
            }
            else if (character == '/' && next == '*')
            {
                blockComment = true;
                index++;
            }
            else if (character is '\'' or '"' or '`')
            {
                quote = character;
            }
            else if (character == '{')
            {
                depth++;
            }
            else if (character == '}' && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    static string? ExtractArrowBody(string source, int callStart, int limit)
    {
        var arrow = source.IndexOf("=>", callStart, limit - callStart, StringComparison.Ordinal);
        if (arrow < 0)
        {
            return null;
        }

        var expressionStart = arrow + 2;
        while (expressionStart < limit && char.IsWhiteSpace(source[expressionStart]))
        {
            expressionStart++;
        }

        var start = expressionStart < limit && source[expressionStart] == '{' ? expressionStart : -1;
        if (start < 0)
        {
            var end = source.IndexOfAny(['\n', ';'], expressionStart);
            var expression = source[expressionStart..(end < 0 ? Math.Min(limit, source.Length) : end)].Trim();
            return expression.EndsWith("))", StringComparison.Ordinal) ? expression[..^1] : expression;
        }

        var endBrace = MatchingBrace(source, start);
        return endBrace < 0 ? null : source[(start + 1)..endBrace].Trim();
    }

    static int LineAt(string source, int index) => source.AsSpan(0, index).Count('\n') + 1;

    [GeneratedRegex("""\bdescribe\s*\(\s*(['"`])([^'"`]+)\1\s*,\s*(?:async\s*)?\(\s*\)\s*=>""", RegexOptions.Multiline)]
    private static partial Regex Describe();

    [GeneratedRegex("""\b(?:it|test)\s*\(\s*(['"`])([^'"`]+)\1\s*,""", RegexOptions.Multiline)]
    private static partial Regex Test();

    [GeneratedRegex(@"\bbeforeEach\s*\(", RegexOptions.Multiline)]
    private static partial Regex BeforeEach();

    sealed record Block(string Title, int Start, int End);
}
