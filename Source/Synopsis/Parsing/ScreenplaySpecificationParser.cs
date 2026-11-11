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
        var scenarios = files.SelectMany(file => ParseFile(file, context)).ToList();
        return new(scenarios, []);
    }

    static IEnumerable<BehaviorScenario> ParseFile(SourceFile file, DiscoveryContext context)
    {
        var lines = file.Content.Replace("\r\n", "\n").Split('\n');
        string? module = null;
        string? feature = null;
        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (TryValue(trimmed, "module ", out var value))
            {
                module = value;
            }
            else if (TryValue(trimmed, "feature ", out value))
            {
                feature = value;
            }
            else if (TryValue(trimmed, "specification ", out value))
            {
                var specification = ReadSpecification(lines, index, value, new(module, feature));
                yield return ToScenario(file, context, specification);
                index = specification.End - 1;
            }
        }
    }

    static bool TryValue(string trimmed, string prefix, out string value)
    {
        var matches = trimmed.StartsWith(prefix, StringComparison.Ordinal);
        value = matches ? trimmed[prefix.Length..].Trim() : string.Empty;
        return matches;
    }

    static Specification ReadSpecification(string[] lines, int index, string name, Header header)
    {
        var indent = Indent(lines[index]);
        var steps = new Steps();
        var end = index + 1;
        for (; end < lines.Length; end++)
        {
            var child = lines[end];
            if (string.IsNullOrWhiteSpace(child))
            {
                continue;
            }
            if (Indent(child) <= indent)
            {
                break;
            }
            steps.Add(child.Trim());
        }
        return new(name, index + 1, header, steps, end);
    }

    static BehaviorScenario ToScenario(SourceFile file, DiscoveryContext context, Specification specification)
    {
        var classification = context.Classify(file.ClassificationPath, specification.Header.Module, specification.Header.Feature);
        var title = Humanizer.Identifier(specification.Name);
        return new(
            $"play:{file.RelativePath}:{specification.Line}",
            classification.Module,
            classification.Feature,
            classification.Subject,
            title,
            specification.Steps.Given,
            specification.Steps.When ?? new(title),
            specification.Steps.Then,
            "Screenplay",
            "Model",
            context.Locate(file.RelativePath, specification.Line));
    }

    static int Indent(string line) => line.Length - line.TrimStart().Length;

    static string Head(string statement)
    {
        var value = statement.Trim();
        var split = value.IndexOfAny([' ', '{']);
        return split < 0 ? value : value[..split];
    }

    sealed record Header(string? Module, string? Feature);
    sealed record Specification(string Name, int Line, Header Header, Steps Steps, int End);

    sealed class Steps
    {
        public List<BehaviorStep> Given { get; } = [];
        public BehaviorStep? When { get; private set; }
        public List<BehaviorStep> Then { get; } = [];

        public void Add(string statement)
        {
            if (statement.StartsWith("given ", StringComparison.Ordinal))
            {
                Given.Add(Step(statement[6..]));
            }
            else if (statement.StartsWith("when ", StringComparison.Ordinal))
            {
                When = Step(statement[5..]);
            }
            else if (statement.StartsWith("then error", StringComparison.Ordinal))
            {
                var reason = statement[10..].Trim().Trim('"');
                Then.Add(new(reason.Length == 0 ? "An error is returned" : $"The error is ‘{reason}’"));
            }
            else if (statement.StartsWith("then ", StringComparison.Ordinal))
            {
                Then.Add(Step(statement[5..]));
            }
        }

        static BehaviorStep Step(string text) => new(Humanizer.Identifier(Head(text)));
    }
}
