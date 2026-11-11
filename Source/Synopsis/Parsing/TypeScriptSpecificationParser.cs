// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Model;
using static Cratis.Synopsis.Parsing.TypeScriptScanner;

namespace Cratis.Synopsis.Parsing;

internal partial class TypeScriptSpecificationParser : ISpecificationParser
{
    static readonly HashSet<string> SuiteNames = new(StringComparer.Ordinal) { "describe", "context", "suite", "fdescribe", "xdescribe" };
    static readonly HashSet<string> TestNames = new(StringComparer.Ordinal) { "it", "test", "specify", "fit", "xit", "xtest" };
    static readonly HashSet<string> Modifiers = new(StringComparer.Ordinal) { "skip", "only", "todo", "concurrent", "serial", "fails" };
    static readonly string[] FileSuffixes = [".spec", ".test", ".specs", ".tests"];

    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", ".mts", ".cts" };

    public ParseResult Parse(IReadOnlyList<SourceFile> files, DiscoveryContext context)
    {
        var scenarios = new List<BehaviorScenario>();
        var diagnostics = new List<DiscoveryDiagnostic>();
        foreach (var file in files)
        {
            var scope = new FileScope(file, context, context.Classify(file.ClassificationPath));
            var calls = FileCalls.From(Calls(file, diagnostics).ToList());
            scenarios.AddRange(SuiteScenarios(scope, calls));
            if (TopLevelScenario(scope, calls) is { } topLevel)
            {
                scenarios.Add(topLevel);
            }
        }

        return new(scenarios, diagnostics);
    }

    static IEnumerable<BehaviorScenario> SuiteScenarios(FileScope scope, FileCalls calls)
    {
        foreach (var suite in calls.Suites)
        {
            var owned = calls.Tests.Where(test => Owner(test, calls.Suites) == suite).ToList();
            if (owned.Count > 0)
            {
                yield return CreateScenario(scope, SuiteDraft(calls, suite, owned));
            }
        }
    }

    static ScenarioDraft SuiteDraft(FileCalls calls, Call suite, List<Call> owned)
    {
        var ancestors = calls.Suites.Where(parent => Contains(parent, suite)).OrderBy(_ => _.End - _.Start).Reverse().ToList();
        var relevantSuites = ancestors.Append(suite).ToList();
        var given = ancestors
            .Select(_ => new BehaviorStep(Humanizer.Context(_.Title!)))
            .Concat(calls.Hooks.Where(hook => Owner(hook, calls.Suites) is { } owner && relevantSuites.Contains(owner))
                .Select(HookStep))
            .ToList();
        return new(suite.Title!, suite.Start, given, owned);
    }

    static BehaviorScenario? TopLevelScenario(FileScope scope, FileCalls calls)
    {
        var topLevelTests = calls.Tests.Where(test => Owner(test, calls.Suites) is null).ToList();
        if (topLevelTests.Count == 0)
        {
            return null;
        }

        var given = calls.Hooks.Where(hook => Owner(hook, calls.Suites) is null).Select(HookStep).ToList();
        return CreateScenario(scope, new(FileTitle(scope.File.RelativePath), topLevelTests[0].Start, given, topLevelTests));
    }

    static BehaviorStep HookStep(Call hook) => new(HookTitle(hook.Name), hook.Body);

    static BehaviorScenario CreateScenario(FileScope scope, ScenarioDraft draft)
    {
        var file = scope.File;
        var classification = scope.Classification;
        var readableTitle = Humanizer.Scenario(draft.Title);
        var line = LineAt(file.Content, draft.Start);
        return new(
            $"ts:{file.RelativePath}:{line}",
            classification.Module,
            classification.Feature,
            classification.Subject,
            readableTitle,
            draft.Given,
            new(readableTitle),
            draft.Tests.Select(test => new BehaviorStep(test.Title!, test.Body)).ToList(),
            Language(file.RelativePath),
            "Frontend",
            scope.Context.Locate(file.RelativePath, line));
    }

    static IEnumerable<Call> Calls(SourceFile file, List<DiscoveryDiagnostic> diagnostics)
    {
        var code = CodePositions(file.Content);
        foreach (Match candidate in Candidate().Matches(file.Content))
        {
            if (!IsCallCandidate(file.Content, code, candidate))
            {
                continue;
            }

            var site = new CallSite(file.Content, candidate.Index, candidate.Index + candidate.Length, candidate.Value, KindOf(candidate.Value));
            if (ParseOrRecover(site, file.RelativePath, diagnostics) is { } call)
            {
                yield return call;
            }
        }
    }

    static bool IsCallCandidate(string source, bool[] code, Match candidate)
    {
        var qualified = PreviousNonWhitespaceOnLine(source, candidate.Index) == '.';
        return code[candidate.Index] && (!qualified || IsBddQualifier(QualifierBefore(source, candidate.Index)));
    }

    static CallKind KindOf(string name) => SuiteNames.Contains(name) ? CallKind.Suite : TestNames.Contains(name) ? CallKind.Test : CallKind.Hook;

    static Call? ParseOrRecover(CallSite site, string relativePath, List<DiscoveryDiagnostic> diagnostics)
    {
        if (ParseCall(site) is { } call)
        {
            return call;
        }
        if (site.Kind != CallKind.Suite || !LooksLikeTitledSuite(site.Source, site.Cursor))
        {
            return null;
        }

        diagnostics.Add(new(relativePath, $"Could not read the {site.Name} suite at line {LineAt(site.Source, site.Start)}."));
        return RecoverSuite(site);
    }

    static Call? ParseCall(CallSite site)
    {
        var source = site.Source;
        if (!TrySkipModifiers(source, site.Cursor, out var cursor, out var dataDriven) || !At(source, cursor, '('))
        {
            return null;
        }

        var callEnd = Matching(source, cursor, '(', ')');
        if (callEnd < 0)
        {
            return null;
        }

        if (!TryReadTitle(site, SkipWhitespace(source, cursor + 1), out var title, out var callbackStart))
        {
            return null;
        }

        var callback = Callback(source, callbackStart, callEnd);
        if (site.Kind == CallKind.Suite && callback.BodyStart is null)
        {
            return null;
        }

        return new(site.Name, site.Kind, title, site.Start, callback.BodyEnd ?? callEnd, callback.BodyStart, callback.Body, dataDriven);
    }

    static bool TryReadTitle(CallSite site, int start, out string? title, out int end)
    {
        title = null;
        end = start;
        return site.Kind is CallKind.Hook || TryReadString(site.Source, start, out title, out end);
    }

    static bool TrySkipModifiers(string source, int start, out int cursor, out bool dataDriven)
    {
        cursor = SkipWhitespace(source, start);
        dataDriven = false;
        while (At(source, cursor, '.'))
        {
            var propertyStart = cursor + 1;
            cursor = SkipIdentifier(source, propertyStart);
            var property = source[propertyStart..cursor];
            if (property.Equals("each", StringComparison.Ordinal))
            {
                dataDriven = true;
                if (!TrySkipEachData(source, SkipWhitespace(source, cursor), out cursor))
                {
                    return false;
                }
            }
            else if (!Modifiers.Contains(property))
            {
                return false;
            }
            cursor = SkipWhitespace(source, cursor);
        }
        return true;
    }

    static bool TrySkipEachData(string source, int cursor, out int end)
    {
        end = cursor;
        if (At(source, cursor, '('))
        {
            var dataEnd = Matching(source, cursor, '(', ')');
            end = dataEnd + 1;
            return dataEnd >= 0;
        }
        if (At(source, cursor, '`'))
        {
            end = StringEnd(source, cursor);
            return end >= 0;
        }
        return false;
    }

    static CallbackInfo Callback(string source, int start, int limit)
    {
        var length = Math.Max(0, limit - start);
        var arrow = source.IndexOf("=>", start, length, StringComparison.Ordinal);
        var function = Function().Match(source, start, length);
        if (arrow < 0 && !function.Success)
        {
            return new(null, null, null);
        }

        var expressionStart = SkipWhitespace(source, MarkerEnd(function, arrow));
        return expressionStart < limit && source[expressionStart] == '{'
            ? BlockBody(source, expressionStart, limit)
            : ExpressionBody(source, expressionStart, limit);
    }

    static int MarkerEnd(Match function, int arrow) =>
        function.Success && (arrow < 0 || function.Index < arrow) ? function.Index + function.Length : arrow + 2;

    static CallbackInfo BlockBody(string source, int bodyStart, int limit)
    {
        var end = Matching(source, bodyStart, '{', '}');
        return end < 0 || end > limit
            ? new(null, null, null)
            : new(bodyStart, end, source[(bodyStart + 1)..end].Trim());
    }

    static CallbackInfo ExpressionBody(string source, int expressionStart, int limit)
    {
        var expressionEnd = TopLevelArgumentEnd(source, expressionStart, limit);
        var body = expressionStart < expressionEnd ? source[expressionStart..expressionEnd].Trim() : null;
        return new(null, null, body);
    }

    static bool IsBddQualifier(string qualifier) => qualifier is "test" or "vitest" or "jest" or "mocha";

    static Call? Owner(Call child, IReadOnlyList<Call> suites) => suites
        .Where(suite => child.Start > suite.BodyStart && child.Start < suite.End)
        .OrderBy(suite => suite.End - suite.Start)
        .FirstOrDefault();

    static bool Contains(Call parent, Call child) => child.Start > parent.BodyStart && child.End < parent.End;

    static bool LooksLikeTitledSuite(string source, int cursor)
    {
        cursor = SkipWhitespace(source, cursor);
        while (At(source, cursor, '.'))
        {
            var start = cursor + 1;
            cursor = SkipLetters(source, start);
            if (!Modifiers.Contains(source[start..cursor]))
            {
                return false;
            }
            cursor = SkipWhitespace(source, cursor);
        }
        if (!At(source, cursor, '('))
        {
            return false;
        }

        cursor = SkipWhitespace(source, cursor + 1);
        return cursor < source.Length && IsQuote(source[cursor]);
    }

    static Call? RecoverSuite(CallSite site)
    {
        var source = site.Source;
        var cursor = SkipWhitespace(source, site.Cursor);
        while (At(source, cursor, '.'))
        {
            cursor = SkipWhitespace(source, SkipLetters(source, cursor + 1));
        }
        if (!At(source, cursor, '(') || !TryReadString(source, SkipWhitespace(source, cursor + 1), out var title, out cursor))
        {
            return null;
        }

        var arrow = source.IndexOf("=>", cursor, StringComparison.Ordinal);
        var function = Function().Match(source, cursor);
        if (arrow < 0 && !function.Success)
        {
            return null;
        }

        var bodyStart = SkipWhitespace(source, MarkerEnd(function, arrow));
        return At(source, bodyStart, '{')
            ? new(site.Name, CallKind.Suite, title, site.Start, source.Length, bodyStart, source[(bodyStart + 1)..].Trim(), false)
            : null;
    }

    static string HookTitle(string name) => name switch
    {
        "beforeAll" or "before" or "suiteSetup" => "The suite context",
        _ => "The scenario context"
    };

    static string Language(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".tsx" => "TSX",
        ".jsx" => "JSX",
        ".js" or ".mjs" or ".cjs" => "JavaScript",
        _ => "TypeScript"
    };

    static string FileTitle(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var suffix = FileSuffixes.FirstOrDefault(_ => name.EndsWith(_, StringComparison.OrdinalIgnoreCase));
        if (suffix is not null)
        {
            name = name[..^suffix.Length];
        }
        if (StartsWithWord(name, "and") && ParentScenarioFolder(path) is { } parent)
        {
            return $"{Humanizer.Scenario(parent)} and {Humanizer.Identifier(name[4..]).ToLowerInvariant()}";
        }
        return Humanizer.Scenario(name);
    }

    static string? ParentScenarioFolder(string path) => path.Replace('\\', '/').Split('/').SkipLast(1).LastOrDefault(_ => StartsWithWord(_, "when"));

    static bool StartsWithWord(string name, string word) =>
        name.StartsWith($"{word}_", StringComparison.OrdinalIgnoreCase) || name.StartsWith($"{word}-", StringComparison.OrdinalIgnoreCase);

    static int LineAt(string source, int index) => source.AsSpan(0, index).Count('\n') + 1;

    [GeneratedRegex(@"\b(?:describe|context|suite|fdescribe|xdescribe|it|test|specify|fit|xit|xtest|beforeEach|beforeAll|before|setup|suiteSetup)\b", RegexOptions.Multiline)]
    private static partial Regex Candidate();

    [GeneratedRegex(@"\bfunction\b(?:\s+[A-Za-z_$][\w$]*)?\s*\([^)]*\)\s*", RegexOptions.Multiline)]
    private static partial Regex Function();

    enum CallKind { Suite, Test, Hook }
    sealed record Call(string Name, CallKind Kind, string? Title, int Start, int End, int? BodyStart, string? Body, bool DataDriven);
    sealed record CallbackInfo(int? BodyStart, int? BodyEnd, string? Body);
    sealed record CallSite(string Source, int Start, int Cursor, string Name, CallKind Kind);
    sealed record FileScope(SourceFile File, DiscoveryContext Context, PathClassification Classification);
    sealed record ScenarioDraft(string Title, int Start, IReadOnlyList<BehaviorStep> Given, IReadOnlyList<Call> Tests);

    sealed record FileCalls(IReadOnlyList<Call> Suites, IReadOnlyList<Call> Tests, IReadOnlyList<Call> Hooks)
    {
        public static FileCalls From(IReadOnlyList<Call> calls) => new(
            calls.Where(_ => _.Kind == CallKind.Suite && _.BodyStart is not null).ToList(),
            calls.Where(_ => _.Kind == CallKind.Test && _.Title is not null).ToList(),
            calls.Where(_ => _.Kind == CallKind.Hook).ToList());
    }
}
