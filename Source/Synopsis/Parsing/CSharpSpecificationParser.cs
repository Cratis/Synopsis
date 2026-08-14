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
    static readonly HashSet<string> TestAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fact", "Theory",                                      // xUnit
        "Test", "TestCase", "TestCaseSource", "TestTemplate", // NUnit and TUnit
        "TestMethod", "DataTestMethod",                         // MSTest
        "RowTest", "Property", "Scenario"                       // MbUnit, property-based suites, and LightBDD
    };
    static readonly HashSet<string> GivenMethodNames = new(StringComparer.OrdinalIgnoreCase) { "Establish", "Given", "Arrange", "SetUp", "Setup", "TestInitialize", "BeforeEach" };
    static readonly HashSet<string> GivenAttributes = new(StringComparer.OrdinalIgnoreCase) { "SetUp", "OneTimeSetUp", "TestInitialize", "ClassInitialize" };
    static readonly HashSet<string> WhenMethodNames = new(StringComparer.OrdinalIgnoreCase) { "Because", "When", "Act" };
    static readonly HashSet<string> IgnoredBaseTypes = new(StringComparer.OrdinalIgnoreCase) { "Specification", "IClassFixture", "ICollectionFixture", "IAsyncLifetime", "IDisposable", "IAsyncDisposable" };

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

            var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
                .Where(_ => _ is ClassDeclarationSyntax or RecordDeclarationSyntax)
                .ToList();
            parsed.Add(new(file, tree, types));
        }

        var classes = parsed.SelectMany(file => file.Classes.Select(type => new ClassInfo(file, type))).ToList();
        var scenarios = new List<BehaviorScenario>();
        foreach (var candidate in classes)
        {
            var outcomes = Outcomes(candidate.Type).ToList();
            if (outcomes.Count == 0)
            {
                continue;
            }

            var classification = context.Classify(candidate.File.File.ClassificationPath, namespaceName: candidate.Namespace);
            var given = BuildGiven(candidate, classes);
            var because = FindLifecycle(candidate.Type, WhenMethodNames, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var title = ScenarioTitle(candidate);
            var line = candidate.File.Tree.GetLineSpan(candidate.Type.Identifier.Span).StartLinePosition.Line + 1;
            var location = context.Locate(candidate.File.File.RelativePath, line);
            scenarios.Add(new(
                StableId(candidate.File.File.RelativePath, line),
                classification.Module,
                classification.Feature,
                classification.Subject,
                title,
                given,
                new(title, because?.Details),
                outcomes,
                "C#",
                "Backend",
                location));
        }

        return new(scenarios, diagnostics);
    }

    static IEnumerable<BehaviorStep> Outcomes(TypeDeclarationSyntax type)
    {
        foreach (var method in type.Members.OfType<MethodDeclarationSyntax>().Where(IsTest))
        {
            yield return new(DisplayName(method) ?? Humanizer.Outcome(method.Identifier.ValueText), Body(method));
        }

        // Machine.Specifications expresses assertions as delegate fields:
        //     It should_save_the_order = () => ...;
        foreach (var field in type.Members.OfType<FieldDeclarationSyntax>().Where(_ => TypeName(_.Declaration.Type).Equals("It", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var variable in field.Declaration.Variables)
            {
                yield return new(Humanizer.Outcome(variable.Identifier.ValueText), Body(variable.Initializer?.Value));
            }
        }
    }

    static bool IsTest(MethodDeclarationSyntax method) => method.AttributeLists.SelectMany(_ => _.Attributes).Any(attribute => TestAttributes.Contains(AttributeName(attribute)));

    static string? DisplayName(MethodDeclarationSyntax method)
    {
        foreach (var argument in method.AttributeLists.SelectMany(_ => _.Attributes).SelectMany(_ => _.ArgumentList?.Arguments ?? []))
        {
            var name = argument.NameEquals?.Name.Identifier.ValueText ?? argument.NameColon?.Name.Identifier.ValueText;
            if (name is not null &&
                name is "DisplayName" or "TestName" or "Description" &&
                argument.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }

        return null;
    }

    static IReadOnlyList<BehaviorStep> BuildGiven(ClassInfo candidate, IReadOnlyList<ClassInfo> allClasses)
    {
        var result = new List<BehaviorStep>();
        var visited = new HashSet<TypeDeclarationSyntax>();

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
                if (IgnoredBaseTypes.Contains(shortName))
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
                    result.Add(new(Humanizer.Context(shortName)));
                }
            }

            var establish = FindLifecycle(current.Type, GivenMethodNames, GivenAttributes);
            if (includeName || establish is not null)
            {
                result.Add(new(includeName ? Humanizer.Context(current.Type.Identifier.ValueText) : "The scenario context", establish?.Details));
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
        if (!IsAnd(name))
        {
            return Humanizer.Scenario(WithoutTestSuffix(name));
        }

        var folders = candidate.File.File.RelativePath.Replace('\\', '/').Split('/').SkipLast(1);
        var namespaceSegments = candidate.Namespace.Split('.');
        var enclosingTypes = candidate.Type.Ancestors().OfType<TypeDeclarationSyntax>().Select(_ => _.Identifier.ValueText);
        var parent = folders.Concat(namespaceSegments).Concat(enclosingTypes).LastOrDefault(IsWhen);
        return parent is null
            ? Humanizer.Identifier(StripAnd(name))
            : $"{Humanizer.Scenario(parent)} and {Humanizer.Identifier(StripAnd(name)).ToLowerInvariant()}";
    }

    static bool IsWhen(string value) => value.StartsWith("when_", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("when ", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("When", StringComparison.Ordinal) && value.Length > 4 && char.IsUpper(value[4]);

    static bool IsAnd(string value) => value.StartsWith("and_", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("and ", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("And", StringComparison.Ordinal) && value.Length > 3 && char.IsUpper(value[3]);

    static string StripAnd(string value) => value[(value.StartsWith("and_", StringComparison.OrdinalIgnoreCase) || value.StartsWith("and ", StringComparison.OrdinalIgnoreCase) ? 4 : 3)..];

    static string WithoutTestSuffix(string value)
    {
        foreach (var suffix in new[] { "Specifications", "Specification", "Specs", "Tests", "Test" })
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && value.Length > suffix.Length)
            {
                return value[..^suffix.Length];
            }
        }
        return value;
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

    static Lifecycle? FindLifecycle(TypeDeclarationSyntax type, IReadOnlySet<string> names, IReadOnlySet<string> attributes)
    {
        var method = type.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(_ =>
            names.Contains(_.Identifier.ValueText) ||
            _.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attributes.Contains(AttributeName(attribute))));
        if (method is not null)
        {
            return new(method.Identifier.ValueText, Body(method));
        }

        foreach (var field in type.Members.OfType<FieldDeclarationSyntax>().Where(_ => names.Contains(TypeName(_.Declaration.Type))))
        {
            var variable = field.Declaration.Variables.FirstOrDefault();
            if (variable is not null)
            {
                return new(variable.Identifier.ValueText, Body(variable.Initializer?.Value));
            }
        }

        return null;
    }

    static string TypeName(TypeSyntax type) => type.ToString().Split('.').Last().Split('<').First();

    static string AttributeName(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString().Split('.').Last();
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^9] : name;
    }

    static string? Body(ExpressionSyntax? expression)
    {
        if (expression is null)
        {
            return null;
        }

        if (expression is ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax)
        {
            var lambda = (LambdaExpressionSyntax)expression;
            return lambda.Body is BlockSyntax block ? block.Statements.ToFullString().Trim() : lambda.Body.ToFullString().Trim();
        }

        return expression.ToFullString().Trim();
    }

    static string StableId(string path, int line) => $"cs:{path}:{line}";

    sealed record ParsedFile(SourceFile File, SyntaxTree Tree, IReadOnlyList<TypeDeclarationSyntax> Classes);
    sealed record ClassInfo(ParsedFile File, TypeDeclarationSyntax Type)
    {
        public string Namespace => Type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty;

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
    sealed record Lifecycle(string Name, string? Details);
}
