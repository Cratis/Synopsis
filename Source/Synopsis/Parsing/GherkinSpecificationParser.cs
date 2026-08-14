// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Parsing;

internal class GherkinSpecificationParser : ISpecificationParser
{
    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".feature" };

    public ParseResult Parse(IReadOnlyList<SourceFile> files, DiscoveryContext context)
    {
        var scenarios = new List<BehaviorScenario>();
        var diagnostics = new List<DiscoveryDiagnostic>();
        foreach (var file in files)
        {
            ParseFile(file, context, scenarios, diagnostics);
        }
        return new(scenarios, diagnostics);
    }

    static void ParseFile(SourceFile file, DiscoveryContext context, List<BehaviorScenario> result, List<DiscoveryDiagnostic> diagnostics)
    {
        var initialScenarioCount = result.Count;
        var lines = file.Content.Replace("\r\n", "\n").Split('\n');
        var feature = Path.GetFileNameWithoutExtension(file.RelativePath);
        string? rule = null;
        Scenario? scenario = null;
        List<Step>? background = null;
        var backgrounds = new Dictionary<string, List<Step>>(StringComparer.Ordinal);
        var phase = Phase.Given;
        var inExamples = false;
        IReadOnlyList<string>? exampleHeader = null;

        void FinishScenario()
        {
            if (scenario is null)
            {
                return;
            }

            AddScenarios(file, context, feature, scenario, backgrounds.GetValueOrDefault(scenario.Rule ?? string.Empty) ?? backgrounds.GetValueOrDefault(string.Empty) ?? [], result);
            scenario = null;
            inExamples = false;
            exampleHeader = null;
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var text = lines[index].Trim();
            if (text.Length == 0 || text.StartsWith('#') || text.StartsWith('@'))
            {
                continue;
            }

            if (TryDeclaration(text, "Feature", out var value))
            {
                FinishScenario();
                feature = value;
                rule = null;
                continue;
            }
            if (TryDeclaration(text, "Rule", out value))
            {
                FinishScenario();
                rule = value;
                continue;
            }
            if (TryDeclaration(text, "Background", out _))
            {
                FinishScenario();
                background = [];
                backgrounds[rule ?? string.Empty] = background;
                phase = Phase.Given;
                continue;
            }
            if (TryScenario(text, out value, out var outline))
            {
                FinishScenario();
                background = null;
                scenario = new(value, index + 1, rule, outline);
                phase = Phase.Given;
                continue;
            }
            if (TryDeclaration(text, "Examples", out _))
            {
                if (scenario is not null)
                {
                    inExamples = true;
                    exampleHeader = null;
                }
                continue;
            }
            if (inExamples && text.StartsWith('|') && text.EndsWith('|') && scenario is not null)
            {
                var cells = text[1..^1].Split('|').Select(_ => _.Trim()).ToList();
                if (exampleHeader is null)
                {
                    exampleHeader = cells;
                }
                else if (cells.Count == exampleHeader.Count)
                {
                    scenario.Examples.Add(exampleHeader.Zip(cells).ToDictionary(_ => _.First, _ => _.Second, StringComparer.Ordinal));
                }
                continue;
            }

            if (TryStep(text, ref phase, out var step))
            {
                if (scenario is not null)
                {
                    scenario.Steps.Add(step);
                }
                else if (background is not null)
                {
                    background.Add(step);
                }
            }
        }

        FinishScenario();
        if (result.Count == initialScenarioCount)
        {
            diagnostics.Add(new(file.RelativePath, "The feature file contains no scenarios."));
        }
    }

    static void AddScenarios(SourceFile file, DiscoveryContext context, string feature, Scenario scenario, IReadOnlyList<Step> background, List<BehaviorScenario> result)
    {
        var examples = scenario.Examples.Count > 0 ? scenario.Examples : [new Dictionary<string, string>()];
        for (var exampleIndex = 0; exampleIndex < examples.Count; exampleIndex++)
        {
            var values = examples[exampleIndex];
            var title = Replace(scenario.Title, values);
            var allSteps = background.Concat(scenario.Steps).Select(step => step with { Text = Replace(step.Text, values) }).ToList();
            var given = allSteps.Where(_ => _.Phase == Phase.Given).Select(_ => new BehaviorStep(_.Text)).ToList();
            if (scenario.Examples.Count > 0)
            {
                given.Add(new("Example", string.Join(", ", values.Select(_ => $"{_.Key} = {_.Value}"))));
            }
            var whenSteps = allSteps.Where(_ => _.Phase == Phase.When).Select(_ => _.Text).ToList();
            var then = allSteps.Where(_ => _.Phase == Phase.Then).Select(_ => new BehaviorStep(_.Text)).ToList();
            var classification = context.Classify(file.ClassificationPath, explicitFeature: scenario.Rule is null ? feature : $"{feature} · {scenario.Rule}");
            var subject = scenario.Rule is null ? Humanizer.Identifier(Path.GetFileNameWithoutExtension(file.RelativePath)) : Humanizer.Identifier(scenario.Rule);
            result.Add(new(
                $"gherkin:{file.RelativePath}:{scenario.Line}:{exampleIndex}",
                classification.Module,
                classification.Feature,
                subject,
                Humanizer.Identifier(title),
                given,
                new(whenSteps.Count == 0 ? Humanizer.Identifier(title) : string.Join(" and ", whenSteps.Select((_, index) => index == 0 ? _ : LowerFirst(_)))),
                then,
                "Gherkin",
                "Model",
                context.Locate(file.RelativePath, scenario.Line)));
        }
    }

    static bool TryScenario(string text, out string value, out bool outline)
    {
        foreach (var keyword in new[] { "Scenario Outline", "Scenario Template", "Scenario", "Example" })
        {
            if (TryDeclaration(text, keyword, out value))
            {
                outline = keyword is "Scenario Outline" or "Scenario Template";
                return true;
            }
        }
        value = string.Empty;
        outline = false;
        return false;
    }

    static bool TryDeclaration(string text, string keyword, out string value)
    {
        var prefix = $"{keyword}:";
        if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = text[prefix.Length..].Trim();
            return true;
        }
        value = string.Empty;
        return false;
    }

    static bool TryStep(string text, ref Phase phase, out Step step)
    {
        foreach (var keyword in new[] { "Given", "When", "Then", "And", "But", "*" })
        {
            if (!text.StartsWith($"{keyword} ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (keyword.Equals("Given", StringComparison.OrdinalIgnoreCase)) phase = Phase.Given;
            if (keyword.Equals("When", StringComparison.OrdinalIgnoreCase)) phase = Phase.When;
            if (keyword.Equals("Then", StringComparison.OrdinalIgnoreCase)) phase = Phase.Then;
            step = new(phase, text[(keyword.Length + 1)..].Trim());
            return true;
        }
        step = default;
        return false;
    }

    static string Replace(string value, IReadOnlyDictionary<string, string> examples)
    {
        foreach (var (name, replacement) in examples)
        {
            value = value.Replace($"<{name}>", replacement, StringComparison.Ordinal);
        }
        return value;
    }

    static string LowerFirst(string value) => value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];

    enum Phase { Given, When, Then }
    readonly record struct Step(Phase Phase, string Text);
    sealed record Scenario(string Title, int Line, string? Rule, bool Outline)
    {
        public List<Step> Steps { get; } = [];
        public List<Dictionary<string, string>> Examples { get; } = [];
    }
}
