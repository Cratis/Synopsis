// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Parsing;

internal class ScreenplaySpecificationParser : ISpecificationParser
{
    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".play" };

    public ParseResult Parse(IReadOnlyList<SourceFile> files, DiscoveryContext context)
    {
        var scenarios = new List<BehaviorScenario>();
        foreach (var file in files)
        {
            var lines = file.Content.Replace("\r\n", "\n").Split('\n');
            string? module = null;
            string? feature = null;
            for (var index = 0; index < lines.Length; index++)
            {
                var trimmed = lines[index].Trim();
                if (trimmed.StartsWith("module ", StringComparison.Ordinal))
                {
                    module = trimmed[7..].Trim();
                }
                else if (trimmed.StartsWith("feature ", StringComparison.Ordinal))
                {
                    feature = trimmed[8..].Trim();
                }
                else if (trimmed.StartsWith("specification ", StringComparison.Ordinal))
                {
                    var specIndent = Indent(lines[index]);
                    var name = trimmed[14..].Trim();
                    var given = new List<BehaviorStep>();
                    var then = new List<BehaviorStep>();
                    BehaviorStep? when = null;
                    var end = index + 1;
                    for (; end < lines.Length; end++)
                    {
                        var child = lines[end];
                        if (string.IsNullOrWhiteSpace(child))
                        {
                            continue;
                        }
                        if (Indent(child) <= specIndent)
                        {
                            break;
                        }

                        var statement = child.Trim();
                        if (statement.StartsWith("given ", StringComparison.Ordinal))
                        {
                            given.Add(new(Humanizer.Identifier(Head(statement[6..]))));
                        }
                        else if (statement.StartsWith("when ", StringComparison.Ordinal))
                        {
                            when = new(Humanizer.Identifier(Head(statement[5..])));
                        }
                        else if (statement.StartsWith("then error", StringComparison.Ordinal))
                        {
                            var reason = statement[10..].Trim().Trim('"');
                            then.Add(new(reason.Length == 0 ? "An error is returned" : $"The error is ‘{reason}’"));
                        }
                        else if (statement.StartsWith("then ", StringComparison.Ordinal))
                        {
                            then.Add(new(Humanizer.Identifier(Head(statement[5..]))));
                        }
                    }

                    var classification = context.Classify(file.ClassificationPath, module, feature);
                    var line = index + 1;
                    scenarios.Add(new(
                        $"play:{file.RelativePath}:{line}",
                        classification.Module,
                        classification.Feature,
                        classification.Subject,
                        Humanizer.Identifier(name),
                        given,
                        when ?? new(Humanizer.Identifier(name)),
                        then,
                        "Screenplay",
                        "Model",
                        context.Locate(file.RelativePath, line)));
                    index = end - 1;
                }
            }
        }

        return new(scenarios, []);
    }

    static int Indent(string line) => line.Length - line.TrimStart().Length;

    static string Head(string statement)
    {
        var value = statement.Trim();
        var split = value.IndexOfAny([' ', '{']);
        return split < 0 ? value : value[..split];
    }
}
