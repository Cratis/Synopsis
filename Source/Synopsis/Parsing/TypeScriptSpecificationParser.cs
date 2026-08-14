// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Parsing;

internal partial class TypeScriptSpecificationParser : ISpecificationParser
{
    static readonly HashSet<string> SuiteNames = new(StringComparer.Ordinal) { "describe", "context", "suite", "fdescribe", "xdescribe" };
    static readonly HashSet<string> TestNames = new(StringComparer.Ordinal) { "it", "test", "specify", "fit", "xit", "xtest" };
    static readonly HashSet<string> HookNames = new(StringComparer.Ordinal) { "beforeEach", "beforeAll", "before", "setup", "suiteSetup" };
    static readonly HashSet<string> Modifiers = new(StringComparer.Ordinal) { "skip", "only", "todo", "concurrent", "serial", "fails" };

    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", ".mts", ".cts" };

    public ParseResult Parse(IReadOnlyList<SourceFile> files, DiscoveryContext context)
    {
        var scenarios = new List<BehaviorScenario>();
        var diagnostics = new List<DiscoveryDiagnostic>();
        foreach (var file in files)
        {
            var calls = Calls(file, diagnostics).ToList();
            var suites = calls.Where(_ => _.Kind == CallKind.Suite && _.BodyStart is not null).ToList();
            var tests = calls.Where(_ => _.Kind == CallKind.Test && _.Title is not null).ToList();
            var hooks = calls.Where(_ => _.Kind == CallKind.Hook).ToList();
            var classification = context.Classify(file.ClassificationPath);

            foreach (var suite in suites)
            {
                var owned = tests.Where(test => Owner(test, suites) == suite).ToList();
                if (owned.Count == 0)
                {
                    continue;
                }

                var ancestors = suites.Where(parent => Contains(parent, suite)).OrderBy(_ => _.End - _.Start).Reverse().ToList();
                var relevantSuites = ancestors.Append(suite).ToList();
                var given = ancestors
                    .Select(_ => new BehaviorStep(Humanizer.Context(_.Title!)))
                    .Concat(hooks.Where(hook => Owner(hook, suites) is { } owner && relevantSuites.Contains(owner))
                        .Select(hook => new BehaviorStep(HookTitle(hook.Name), hook.Body)))
                    .ToList();
                scenarios.Add(CreateScenario(file, context, classification, suite.Title!, suite.Start, given, owned));
            }

            var topLevelTests = tests.Where(test => Owner(test, suites) is null).ToList();
            if (topLevelTests.Count > 0)
            {
                var title = FileTitle(file.RelativePath);
                var given = hooks.Where(hook => Owner(hook, suites) is null)
                    .Select(hook => new BehaviorStep(HookTitle(hook.Name), hook.Body))
                    .ToList();
                scenarios.Add(CreateScenario(file, context, classification, title, topLevelTests[0].Start, given, topLevelTests));
            }
        }

        return new(scenarios, diagnostics);
    }

    static BehaviorScenario CreateScenario(SourceFile file, DiscoveryContext context, PathClassification classification, string title, int start, IReadOnlyList<BehaviorStep> given, IReadOnlyList<Call> tests)
    {
        var readableTitle = Humanizer.Scenario(title);
        var line = LineAt(file.Content, start);
        return new(
            $"ts:{file.RelativePath}:{line}",
            classification.Module,
            classification.Feature,
            classification.Subject,
            readableTitle,
            given,
            new(readableTitle),
            tests.Select(test => new BehaviorStep(test.Title!, test.Body)).ToList(),
            Language(file.RelativePath),
            "Frontend",
            context.Locate(file.RelativePath, line));
    }

    static IEnumerable<Call> Calls(SourceFile file, List<DiscoveryDiagnostic> diagnostics)
    {
        var code = CodePositions(file.Content);
        foreach (Match candidate in Candidate().Matches(file.Content))
        {
            var qualified = PreviousNonWhitespaceOnLine(file.Content, candidate.Index) == '.';
            if (!code[candidate.Index] || qualified && !IsBddQualifier(QualifierBefore(file.Content, candidate.Index)))
            {
                continue;
            }
            var name = candidate.Value;
            var kind = SuiteNames.Contains(name) ? CallKind.Suite : TestNames.Contains(name) ? CallKind.Test : CallKind.Hook;
            var call = ParseCall(file.Content, candidate.Index, candidate.Index + candidate.Length, name, kind);
            if (call is not null)
            {
                yield return call;
            }
            else if (kind == CallKind.Suite && LooksLikeTitledSuite(file.Content, candidate.Index + candidate.Length))
            {
                diagnostics.Add(new(file.RelativePath, $"Could not read the {name} suite at line {LineAt(file.Content, candidate.Index)}."));
                if (RecoverSuite(file.Content, candidate.Index, candidate.Index + candidate.Length, name) is { } recovered)
                {
                    yield return recovered;
                }
            }
        }
    }

    static Call? ParseCall(string source, int start, int cursor, string name, CallKind kind)
    {
        cursor = SkipWhitespace(source, cursor);
        var dataDriven = false;
        while (cursor < source.Length && source[cursor] == '.')
        {
            cursor++;
            var propertyStart = cursor;
            while (cursor < source.Length && (char.IsLetterOrDigit(source[cursor]) || source[cursor] is '_' or '$'))
            {
                cursor++;
            }

            var property = source[propertyStart..cursor];
            if (property.Equals("each", StringComparison.Ordinal))
            {
                dataDriven = true;
                cursor = SkipWhitespace(source, cursor);
                if (cursor < source.Length && source[cursor] == '(')
                {
                    var dataEnd = Matching(source, cursor, '(', ')');
                    if (dataEnd < 0)
                    {
                        return null;
                    }
                    cursor = dataEnd + 1;
                }
                else if (cursor < source.Length && source[cursor] == '`')
                {
                    cursor = StringEnd(source, cursor);
                    if (cursor < 0)
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else if (!Modifiers.Contains(property))
            {
                return null;
            }
            cursor = SkipWhitespace(source, cursor);
        }

        if (cursor >= source.Length || source[cursor] != '(')
        {
            return null;
        }

        var callOpen = cursor;
        var callEnd = Matching(source, callOpen, '(', ')');
        if (callEnd < 0)
        {
            return null;
        }

        cursor = SkipWhitespace(source, callOpen + 1);
        string? title = null;
        if (kind is CallKind.Suite or CallKind.Test)
        {
            if (!TryReadString(source, cursor, out title, out cursor))
            {
                return null;
            }
        }

        var callback = Callback(source, cursor, callEnd);
        if (kind == CallKind.Suite && callback.BodyStart is null)
        {
            return null;
        }

        return new(name, kind, title, start, callback.BodyEnd ?? callEnd, callback.BodyStart, callback.Body, dataDriven);
    }

    static CallbackInfo Callback(string source, int start, int limit)
    {
        var arrow = source.IndexOf("=>", start, Math.Max(0, limit - start), StringComparison.Ordinal);
        var function = Function().Match(source, start, Math.Max(0, limit - start));
        if (arrow < 0 && !function.Success)
        {
            return new(null, null, null);
        }

        var markerEnd = function.Success && (arrow < 0 || function.Index < arrow) ? function.Index + function.Length : arrow + 2;
        var expressionStart = SkipWhitespace(source, markerEnd);
        if (expressionStart < limit && source[expressionStart] == '{')
        {
            var end = Matching(source, expressionStart, '{', '}');
            return end < 0 || end > limit
                ? new(null, null, null)
                : new(expressionStart, end, source[(expressionStart + 1)..end].Trim());
        }

        var expressionEnd = TopLevelArgumentEnd(source, expressionStart, limit);
        var body = expressionStart < expressionEnd ? source[expressionStart..expressionEnd].Trim() : null;
        return new(null, null, body);
    }

    static int TopLevelArgumentEnd(string source, int start, int limit)
    {
        var round = 0;
        var square = 0;
        var curly = 0;
        for (var index = start; index < limit; index++)
        {
            if (source[index] is '\'' or '"' or '`')
            {
                index = StringEnd(source, index) - 1;
                if (index < 0)
                {
                    return limit;
                }
                continue;
            }

            switch (source[index])
            {
                case '(': round++; break;
                case ')': if (round > 0) round--; break;
                case '[': square++; break;
                case ']': if (square > 0) square--; break;
                case '{': curly++; break;
                case '}': if (curly > 0) curly--; break;
                case ',' when round == 0 && square == 0 && curly == 0: return index;
            }
        }
        return limit;
    }

    static bool TryReadString(string source, int start, out string? value, out int end)
    {
        value = null;
        end = start;
        if (start >= source.Length || source[start] is not ('\'' or '"' or '`'))
        {
            return false;
        }

        var quote = source[start];
        var builder = new StringBuilder();
        var escaped = false;
        for (var index = start + 1; index < source.Length; index++)
        {
            var character = source[index];
            if (!escaped && character == quote)
            {
                value = builder.ToString();
                end = index + 1;
                return true;
            }
            if (!escaped && character == '\\')
            {
                escaped = true;
                continue;
            }
            builder.Append(character);
            escaped = false;
        }
        return false;
    }

    static int Matching(string source, int open, char openCharacter, char closeCharacter)
    {
        var depth = 0;
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
            if (character is '\'' or '"' or '`')
            {
                index = StringEnd(source, index) - 1;
                if (index < 0)
                {
                    return -1;
                }
                continue;
            }
            if (character == '/' && LooksLikeRegexStart(source, index))
            {
                index = RegexEnd(source, index) - 1;
                if (index < 0)
                {
                    return -1;
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
            else if (character == openCharacter)
            {
                depth++;
            }
            else if (character == closeCharacter && --depth == 0)
            {
                return index;
            }
        }
        return -1;
    }

    static int StringEnd(string source, int start)
    {
        var quote = source[start];
        var escaped = false;
        for (var index = start + 1; index < source.Length; index++)
        {
            if (!escaped && source[index] == quote)
            {
                return index + 1;
            }
            escaped = !escaped && source[index] == '\\';
            if (source[index] != '\\')
            {
                escaped = false;
            }
        }
        return -1;
    }

    static int RegexEnd(string source, int start)
    {
        var escaped = false;
        var characterClass = false;
        for (var index = start + 1; index < source.Length; index++)
        {
            var character = source[index];
            if (!escaped && character == '[') characterClass = true;
            if (!escaped && character == ']') characterClass = false;
            if (!escaped && character == '/' && !characterClass)
            {
                index++;
                while (index < source.Length && char.IsLetter(source[index])) index++;
                return index;
            }
            escaped = !escaped && character == '\\';
            if (character != '\\') escaped = false;
        }
        return -1;
    }

    static bool LooksLikeRegexStart(string source, int index)
    {
        if (index + 1 >= source.Length || source[index] != '/' || source[index + 1] is '/' or '*')
        {
            return false;
        }
        var previous = PreviousNonWhitespace(source, index);
        return previous == '\0' || "=(:,[!&|?{};>".Contains(previous);
    }

    static char PreviousNonWhitespace(string source, int index)
    {
        for (var cursor = index - 1; cursor >= 0; cursor--)
        {
            if (!char.IsWhiteSpace(source[cursor])) return source[cursor];
        }
        return '\0';
    }

    static char PreviousNonWhitespaceOnLine(string source, int index)
    {
        for (var cursor = index - 1; cursor >= 0 && source[cursor] != '\n'; cursor--)
        {
            if (!char.IsWhiteSpace(source[cursor])) return source[cursor];
        }
        return '\0';
    }

    static string QualifierBefore(string source, int index)
    {
        var cursor = index - 1;
        while (cursor >= 0 && char.IsWhiteSpace(source[cursor])) cursor--;
        if (cursor < 0 || source[cursor] != '.') return string.Empty;
        cursor--;
        var end = cursor + 1;
        while (cursor >= 0 && (char.IsLetterOrDigit(source[cursor]) || source[cursor] is '_' or '$')) cursor--;
        return source[(cursor + 1)..end];
    }

    static bool IsBddQualifier(string qualifier) => qualifier is "test" or "vitest" or "jest" or "mocha";

    static bool[] CodePositions(string source)
    {
        var result = new bool[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var next = index + 1 < source.Length ? source[index + 1] : '\0';
            if (source[index] == '/' && next == '/')
            {
                while (index < source.Length && source[index] != '\n') index++;
                continue;
            }
            if (source[index] == '/' && next == '*')
            {
                index += 2;
                while (index + 1 < source.Length && !(source[index] == '*' && source[index + 1] == '/')) index++;
                index++;
                continue;
            }
            if (source[index] is '\'' or '"' or '`')
            {
                index = StringEnd(source, index) - 1;
                if (index < 0) break;
                continue;
            }
            if (source[index] == '/' && LooksLikeRegexStart(source, index))
            {
                index = RegexEnd(source, index) - 1;
                if (index < 0) break;
                continue;
            }
            result[index] = true;
        }
        return result;
    }

    static Call? Owner(Call child, IReadOnlyList<Call> suites) => suites
        .Where(suite => child.Start > suite.BodyStart && child.Start < suite.End)
        .OrderBy(suite => suite.End - suite.Start)
        .FirstOrDefault();

    static bool Contains(Call parent, Call child) => child.Start > parent.BodyStart && child.End < parent.End;

    static int SkipWhitespace(string source, int cursor)
    {
        while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
        {
            cursor++;
        }
        return cursor;
    }

    static bool LooksLikeTitledSuite(string source, int cursor)
    {
        cursor = SkipWhitespace(source, cursor);
        while (cursor < source.Length && source[cursor] == '.')
        {
            cursor++;
            var start = cursor;
            while (cursor < source.Length && char.IsLetter(source[cursor])) cursor++;
            if (!Modifiers.Contains(source[start..cursor])) return false;
            cursor = SkipWhitespace(source, cursor);
        }
        if (cursor >= source.Length || source[cursor] != '(') return false;
        cursor = SkipWhitespace(source, cursor + 1);
        return cursor < source.Length && source[cursor] is '\'' or '"' or '`';
    }

    static Call? RecoverSuite(string source, int start, int cursor, string name)
    {
        cursor = SkipWhitespace(source, cursor);
        while (cursor < source.Length && source[cursor] == '.')
        {
            cursor++;
            while (cursor < source.Length && char.IsLetter(source[cursor])) cursor++;
            cursor = SkipWhitespace(source, cursor);
        }
        if (cursor >= source.Length || source[cursor] != '(' || !TryReadString(source, SkipWhitespace(source, cursor + 1), out var title, out cursor))
        {
            return null;
        }

        var arrow = source.IndexOf("=>", cursor, StringComparison.Ordinal);
        var function = Function().Match(source, cursor);
        var markerEnd = function.Success && (arrow < 0 || function.Index < arrow) ? function.Index + function.Length : arrow < 0 ? -1 : arrow + 2;
        if (markerEnd < 0)
        {
            return null;
        }
        var bodyStart = SkipWhitespace(source, markerEnd);
        if (bodyStart >= source.Length || source[bodyStart] != '{')
        {
            return null;
        }
        return new(name, CallKind.Suite, title, start, source.Length, bodyStart, source[(bodyStart + 1)..].Trim(), false);
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
        foreach (var suffix in new[] { ".spec", ".test", ".specs", ".tests" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }
        if (name.StartsWith("and_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("and-", StringComparison.OrdinalIgnoreCase))
        {
            var parent = path.Replace('\\', '/').Split('/').SkipLast(1).LastOrDefault(_ => _.StartsWith("when_", StringComparison.OrdinalIgnoreCase) || _.StartsWith("when-", StringComparison.OrdinalIgnoreCase));
            if (parent is not null)
            {
                return $"{Humanizer.Scenario(parent)} and {Humanizer.Identifier(name[4..]).ToLowerInvariant()}";
            }
        }
        return Humanizer.Scenario(name);
    }

    static int LineAt(string source, int index) => source.AsSpan(0, index).Count('\n') + 1;

    [GeneratedRegex(@"\b(?:describe|context|suite|fdescribe|xdescribe|it|test|specify|fit|xit|xtest|beforeEach|beforeAll|before|setup|suiteSetup)\b", RegexOptions.Multiline)]
    private static partial Regex Candidate();

    [GeneratedRegex(@"\bfunction\b(?:\s+[A-Za-z_$][\w$]*)?\s*\([^)]*\)\s*", RegexOptions.Multiline)]
    private static partial Regex Function();

    enum CallKind { Suite, Test, Hook }
    sealed record Call(string Name, CallKind Kind, string? Title, int Start, int End, int? BodyStart, string? Body, bool DataDriven);
    sealed record CallbackInfo(int? BodyStart, int? BodyEnd, string? Body);
}
