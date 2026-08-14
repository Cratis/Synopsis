// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.Synopsis.Parsing;

internal class CSharpSpecificationParser : ISpecificationParser
{
    static readonly HashSet<string> TestAttributes = new(StringComparer.OrdinalIgnoreCase) { "Fact", "Theory", "Test", "TestCase" };

    public IReadOnlySet<string> Extensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cs" };

    public ParseResult Parse(IReadOnlyList<SourceFile> files, DiscoveryContext context)
    {
        var diagnostics = new List<DiscoveryDiagnostic>();
        var parsed = new List<ParsedFile>();
        foreach (var file in files)
        {
            // Cratis applications intentionally compile their colocated specifications only in Debug builds.
            // Synopsis reads the behavioral source, not the shipping compilation, so those regions must be active.
            var parseOptions = CSharpParseOptions.Default.WithPreprocessorSymbols("DEBUG");
            var tree = CSharpSyntaxTree.ParseText(file.Content, parseOptions, file.RelativePath);
            var root = tree.GetRoot();
            foreach (var diagnostic in tree.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).Take(3))
            {
                diagnostics.Add(new(file.RelativePath, $"C# syntax: {diagnostic.GetMessage()}"));
            }

            parsed.Add(new(file, tree, root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList()));
        }

        var classes = parsed.SelectMany(file => file.Classes.Select(type => new ClassInfo(file, type))).ToList();
        var scenarios = new List<BehaviorScenario>();
        foreach (var candidate in classes)
        {
            var facts = candidate.Type.Members.OfType<MethodDeclarationSyntax>().Where(IsTest).ToList();
            if (facts.Count == 0)
            {
                continue;
            }

            var classification = context.Classify(candidate.File.File.ClassificationPath);
            var given = BuildGiven(candidate, classes);
            var because = candidate.Type.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(_ => _.Identifier.ValueText.Equals("Because", StringComparison.OrdinalIgnoreCase));
            var title = ScenarioTitle(candidate);
            var line = candidate.File.Tree.GetLineSpan(candidate.Type.Identifier.Span).StartLinePosition.Line + 1;
            var location = context.Locate(candidate.File.File.RelativePath, line);
            var outcomes = facts.Select(method => new BehaviorStep(Humanizer.Outcome(method.Identifier.ValueText), Body(method))).ToList();
            scenarios.Add(new(
                StableId(candidate.File.File.RelativePath, line),
                classification.Module,
                classification.Feature,
                classification.Subject,
                title,
                given,
                new(title, because is null ? null : Body(because)),
                outcomes,
                "C#",
                "Backend",
                location));
        }

        return new(scenarios, diagnostics);
    }

    static bool IsTest(MethodDeclarationSyntax method) => method.AttributeLists.SelectMany(_ => _.Attributes).Any(attribute =>
    {
        var name = attribute.Name.ToString().Split('.').Last();
        name = name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^9] : name;
        return TestAttributes.Contains(name);
    });

    static IReadOnlyList<BehaviorStep> BuildGiven(ClassInfo candidate, IReadOnlyList<ClassInfo> allClasses)
    {
        var result = new List<BehaviorStep>();
        var visited = new HashSet<ClassDeclarationSyntax>();

        void AddContext(ClassInfo current, bool includeName)
        {
            if (!visited.Add(current.Type))
            {
                return;
            }

            foreach (var baseType in current.Type.BaseList?.Types ?? [])
            {
                var baseName = baseType.Type.ToString();
                var shortName = baseName.Split('.').Last().Split('<').First();
                if (shortName.Equals("Specification", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var inherited = ResolveBase(current, baseName, shortName, allClasses);
                if (inherited is not null)
                {
                    AddContext(inherited, true);
                }
                else
                {
                    result.Add(new(Humanizer.Identifier(shortName)));
                }
            }

            var establish = current.Type.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(_ => _.Identifier.ValueText.Equals("Establish", StringComparison.OrdinalIgnoreCase));
            if (includeName || establish is not null)
            {
                result.Add(new(includeName ? Humanizer.Identifier(current.Type.Identifier.ValueText) : "The scenario context", establish is null ? null : Body(establish)));
            }
        }

        AddContext(candidate, false);
        return result;
    }

    static ClassInfo? ResolveBase(ClassInfo current, string baseName, string shortName, IReadOnlyList<ClassInfo> allClasses)
    {
        var candidates = allClasses.Where(_ => _.Type.Identifier.ValueText.Equals(shortName, StringComparison.Ordinal)).ToList();
        var currentNamespace = current.Type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty;
        var expected = string.IsNullOrEmpty(currentNamespace) ? baseName : $"{currentNamespace}.{baseName}";
        var exact = candidates.FirstOrDefault(_ => _.FullName.Equals(expected, StringComparison.Ordinal));
        if (exact is not null)
        {
            return exact;
        }

        var currentParts = current.File.File.RelativePath.Split('/');
        return candidates.OrderByDescending(candidate => SharedPrefix(currentParts, candidate.File.File.RelativePath.Split('/'))).FirstOrDefault();
    }

    static int SharedPrefix(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        var count = 0;
        while (count < first.Count && count < second.Count && first[count].Equals(second[count], StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    static string ScenarioTitle(ClassInfo candidate)
    {
        var name = candidate.Type.Identifier.ValueText;
        if (!name.StartsWith("and_", StringComparison.OrdinalIgnoreCase))
        {
            return Humanizer.Identifier(name, "when_");
        }

        var folders = candidate.File.File.RelativePath.Replace('\\', '/').Split('/').SkipLast(1).ToList();
        var parent = folders.LastOrDefault(_ => _.StartsWith("when_", StringComparison.OrdinalIgnoreCase));
        return parent is null
            ? Humanizer.Identifier(name, "and_")
            : $"{Humanizer.Identifier(parent, "when_")} and {Humanizer.Identifier(name, "and_").ToLowerInvariant()}";
    }

    static string? Body(MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody is not null)
        {
            return method.ExpressionBody.Expression.ToFullString().Trim();
        }

        if (method.Body is null)
        {
            return null;
        }

        return method.Body.Statements.ToFullString().Trim();
    }

    static string StableId(string path, int line) => $"cs:{path}:{line}";

    sealed record ParsedFile(SourceFile File, SyntaxTree Tree, IReadOnlyList<ClassDeclarationSyntax> Classes);
    sealed record ClassInfo(ParsedFile File, ClassDeclarationSyntax Type)
    {
        public string FullName
        {
            get
            {
                var type = Type.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(_ => _.Identifier.ValueText).Append(Type.Identifier.ValueText);
                var namespaceName = Type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
                return string.Join('.', string.IsNullOrEmpty(namespaceName) ? type : type.Prepend(namespaceName));
            }
        }
    }
}
