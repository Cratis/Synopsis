// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Parsing;

internal partial class GherkinSpecificationParser : ISpecificationParser
{
    static readonly string[] ScenarioKeywords = ["Scenario Outline", "Scenario Template", "Scenario", "Example"];
    static readonly string[] StepKeywords = ["Given", "When", "Then", "And", "But", "*"];

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
        var reader = new FeatureReader(file, context, result);
        var lines = file.Content.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            reader.Read(index, lines[index].Trim());
        }

        reader.FinishScenario();
        if (result.Count == initialScenarioCount)
        {
            diagnostics.Add(new(file.RelativePath, "The feature file contains no scenarios."));
        }
    }

    static IEnumerable<BehaviorScenario> BuildScenarios(FeatureScope scope, Scenario scenario, IReadOnlyList<Step> background)
    {
        var examples = scenario.Examples.Count > 0 ? scenario.Examples : [new Dictionary<string, string>()];
        for (var exampleIndex = 0; exampleIndex < examples.Count; exampleIndex++)
        {
            yield return BuildScenario(scope, new(scenario, background, examples[exampleIndex], exampleIndex));
        }
    }

    static BehaviorScenario BuildScenario(FeatureScope scope, ExampleRun run)
    {
        var scenario = run.Scenario;
        var file = scope.File;
        var title = Replace(scenario.Title, run.Values);
        var allSteps = run.Background.Concat(scenario.Steps).Select(step => step with { Text = Replace(step.Text, run.Values) }).ToList();
        var given = allSteps.Where(_ => _.Phase == Phase.Given).Select(_ => new BehaviorStep(_.Text)).ToList();
        if (scenario.Examples.Count > 0)
        {
            given.Add(new("Example", string.Join(", ", run.Values.Select(_ => $"{_.Key} = {_.Value}"))));
        }
        var then = allSteps.Where(_ => _.Phase == Phase.Then).Select(_ => new BehaviorStep(_.Text)).ToList();
        var classification = scope.Context.Classify(file.ClassificationPath, explicitFeature: scenario.Rule is null ? scope.Feature : $"{scope.Feature} · {scenario.Rule}");
        return new(
            $"gherkin:{file.RelativePath}:{scenario.Line}:{run.Index}",
            classification.Module,
            classification.Feature,
            SubjectOf(file, scenario),
            Humanizer.Identifier(title),
            given,
            new(WhenText(title, allSteps)),
            then,
            "Gherkin",
            "Model",
            scope.Context.Locate(file.RelativePath, scenario.Line));
    }

    static string SubjectOf(SourceFile file, Scenario scenario) =>
        Humanizer.Identifier(scenario.Rule ?? Path.GetFileNameWithoutExtension(file.RelativePath));

    static string WhenText(string title, IReadOnlyList<Step> steps)
    {
        var whenSteps = steps.Where(_ => _.Phase == Phase.When).Select(_ => _.Text).ToList();
        return whenSteps.Count == 0
            ? Humanizer.Identifier(title)
            : string.Join(" and ", whenSteps.Select((_, index) => index == 0 ? _ : LowerFirst(_)));
    }

    static bool TryScenario(string text, out string value, out bool outline)
    {
        foreach (var keyword in ScenarioKeywords)
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

    static string? StepKeywordOf(string text) =>
        StepKeywords.FirstOrDefault(keyword => text.StartsWith($"{keyword} ", StringComparison.OrdinalIgnoreCase));

    static Phase PhaseOf(string keyword, Phase current) => keyword.ToUpperInvariant() switch
    {
        "GIVEN" => Phase.Given,
        "WHEN" => Phase.When,
        "THEN" => Phase.Then,
        _ => current
    };

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
    sealed record FeatureScope(SourceFile File, DiscoveryContext Context, string Feature);
    sealed record ExampleRun(Scenario Scenario, IReadOnlyList<Step> Background, IReadOnlyDictionary<string, string> Values, int Index);
    sealed record Scenario(string Title, int Line, string? Rule, bool Outline)
    {
        public List<Step> Steps { get; } = [];
        public List<Dictionary<string, string>> Examples { get; } = [];
    }
}
