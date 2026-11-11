// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Discovery;

internal record PathClassification(string Module, string Feature, string Subject);

internal record ClassificationRequest(
    string RelativePath,
    IReadOnlyList<string> Skipped,
    string? ExplicitModule = null,
    string? ExplicitFeature = null,
    string? NamespaceName = null,
    string? FallbackModule = null);

internal static class PathClassifier
{
    static readonly HashSet<string> InfrastructureSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Source", "src", "Core", "DotNET", "JavaScript", "TypeScript", "Specs", "Tests", "Test", "Integration", "Features", "Feature", "Specifications"
    };
    static readonly HashSet<string> OrganizationSegments = new(StringComparer.OrdinalIgnoreCase) { "Cratis", "Hive", "Dolittle" };

    public static PathClassification Classify(ClassificationRequest request)
    {
        var path = BehaviorPath.From(request.RelativePath);
        var subject = Humanizer.Subject(path.SubjectSegment);
        var meaningful = MeaningfulSegments(path, request);

        var fallback = request.FallbackModule is null || request.Skipped.Contains(request.FallbackModule, StringComparer.OrdinalIgnoreCase) ? "System" : Humanizer.Identifier(request.FallbackModule);
        var module = request.ExplicitModule is null ? meaningful.FirstOrDefault() ?? fallback : Humanizer.Identifier(request.ExplicitModule);
        var feature = request.ExplicitFeature is not null ? Humanizer.Identifier(request.ExplicitFeature) : meaningful.Count > 1 ? string.Join(" · ", meaningful.Skip(1)) : subject;
        return new(module, feature, subject);
    }

    static List<string> MeaningfulSegments(BehaviorPath path, ClassificationRequest request)
    {
        var meaningful = SemanticSegments(path.BeforeBehavior.SelectMany(SplitSegment), request.Skipped).ToList();
        if (meaningful.Count > 0 || string.IsNullOrWhiteSpace(request.NamespaceName))
        {
            return meaningful;
        }

        var namespaceSegments = request.NamespaceName.Split('.').TakeWhile(_ => !IsBehaviorSegment(_));
        return SemanticSegments(namespaceSegments, request.Skipped).ToList();
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

    sealed record BehaviorPath(List<string> Segments, int BehaviorIndex)
    {
        public string SubjectSegment => BehaviorIndex >= 0
            ? Segments[BehaviorIndex..].FirstOrDefault(_ => _.StartsWith("for_", StringComparison.OrdinalIgnoreCase)) ?? Segments[BehaviorIndex]
            : Segments.LastOrDefault() ?? "system";

        public IEnumerable<string> BeforeBehavior => BehaviorIndex >= 0 ? Segments[..BehaviorIndex] : Segments;

        public static BehaviorPath From(string relativePath)
        {
            var segments = relativePath.Replace('\\', '/').Split('/').SkipLast(1).ToList();
            return new(segments, segments.FindIndex(_ => _.StartsWith("for_", StringComparison.OrdinalIgnoreCase) || _.StartsWith("when_", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
