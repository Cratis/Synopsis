// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Discovery;

internal record PathClassification(string Module, string Feature, string Subject);

internal static class PathClassifier
{
    static readonly HashSet<string> InfrastructureSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Source", "src", "Core", "DotNET", "JavaScript", "TypeScript", "Specs", "Tests", "Test", "Integration", "Features", "Feature", "Specifications"
    };
    static readonly HashSet<string> OrganizationSegments = new(StringComparer.OrdinalIgnoreCase) { "Cratis", "Hive", "Dolittle" };

    public static PathClassification Classify(
        string relativePath,
        IReadOnlyList<string> skipped,
        string? explicitModule = null,
        string? explicitFeature = null,
        string? namespaceName = null,
        string? fallbackModule = null)
    {
        var segments = relativePath.Replace('\\', '/').Split('/').SkipLast(1).ToList();
        var behaviorIndex = segments.FindIndex(_ => _.StartsWith("for_", StringComparison.OrdinalIgnoreCase) || _.StartsWith("when_", StringComparison.OrdinalIgnoreCase));
        var subjectSegment = behaviorIndex >= 0
            ? segments[behaviorIndex..].FirstOrDefault(_ => _.StartsWith("for_", StringComparison.OrdinalIgnoreCase)) ?? segments[behaviorIndex]
            : segments.LastOrDefault() ?? "system";
        var subject = Humanizer.Subject(subjectSegment);

        var beforeBehavior = behaviorIndex >= 0 ? segments[..behaviorIndex] : segments;
        var meaningful = SemanticSegments(beforeBehavior.SelectMany(SplitSegment), skipped).ToList();
        if (meaningful.Count == 0 && !string.IsNullOrWhiteSpace(namespaceName))
        {
            var namespaceSegments = namespaceName.Split('.').TakeWhile(_ => !IsBehaviorSegment(_));
            meaningful = SemanticSegments(namespaceSegments, skipped).ToList();
        }

        var fallback = fallbackModule is null || skipped.Contains(fallbackModule, StringComparer.OrdinalIgnoreCase) ? "System" : Humanizer.Identifier(fallbackModule);
        var module = explicitModule is null ? meaningful.FirstOrDefault() ?? fallback : Humanizer.Identifier(explicitModule);
        var featureParts = meaningful.Skip(1).ToList();
        var feature = explicitFeature is not null
            ? Humanizer.Identifier(explicitFeature)
            : featureParts.Count > 0
                ? string.Join(" · ", featureParts)
                : subject;
        return new(module, feature, subject);
    }

    static IEnumerable<string> SplitSegment(string segment)
    {
        if (segment.EndsWith(".Specs", StringComparison.OrdinalIgnoreCase) || segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
        {
            return segment.Split('.');
        }
        return [segment];
    }

    static IEnumerable<string> SemanticSegments(IEnumerable<string> segments, IReadOnlyList<string> skipped)
    {
        var result = segments
            .Where(_ => !InfrastructureSegments.Contains(_))
            .Where(_ => !_.StartsWith('.'))
            .Where(_ => !skipped.Contains(_, StringComparer.OrdinalIgnoreCase))
            .Where(_ => !IsBehaviorSegment(_))
            .ToList();
        if (result.Count > 0 && OrganizationSegments.Contains(result[0]))
        {
            result.RemoveAt(0);
        }
        return result.Select(_ => Humanizer.Identifier(_));
    }

    static bool IsBehaviorSegment(string segment) =>
        segment.StartsWith("for_", StringComparison.OrdinalIgnoreCase) ||
        segment.StartsWith("when_", StringComparison.OrdinalIgnoreCase) ||
        segment.StartsWith("given", StringComparison.OrdinalIgnoreCase);
}
