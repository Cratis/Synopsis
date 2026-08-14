// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Discovery;

internal record PathClassification(string Module, string Feature, string Subject);

internal static class PathClassifier
{
    static readonly HashSet<string> InfrastructureSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Source", "src", "Core", "DotNET", "JavaScript", "TypeScript", "Specs", "Tests", "Test", "Integration"
    };

    public static PathClassification Classify(
        string relativePath,
        IReadOnlyList<string> skipped,
        string? explicitModule = null,
        string? explicitFeature = null)
    {
        var segments = relativePath.Replace('\\', '/').Split('/').SkipLast(1).ToList();
        var behaviorIndex = segments.FindIndex(_ => _.StartsWith("for_", StringComparison.OrdinalIgnoreCase) || _.StartsWith("when_", StringComparison.OrdinalIgnoreCase));
        var subjectSegment = behaviorIndex >= 0
            ? segments[behaviorIndex..].FirstOrDefault(_ => _.StartsWith("for_", StringComparison.OrdinalIgnoreCase)) ?? segments[behaviorIndex]
            : segments.LastOrDefault() ?? "system";
        var subject = Humanizer.Identifier(subjectSegment, "for_", "when_");

        var beforeBehavior = behaviorIndex >= 0 ? segments[..behaviorIndex] : segments;
        var meaningful = beforeBehavior
            .Where(_ => !InfrastructureSegments.Contains(_))
            .Where(_ => !_.StartsWith('.'))
            .Where(_ => !skipped.Contains(_, StringComparer.OrdinalIgnoreCase))
            .Where(_ => !_.EndsWith(".Specs", StringComparison.OrdinalIgnoreCase) && !_.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
            .Select(_ => Humanizer.Identifier(_))
            .ToList();

        var module = explicitModule is null ? meaningful.FirstOrDefault() ?? "System" : Humanizer.Identifier(explicitModule);
        var featureParts = meaningful.Skip(1).ToList();
        var feature = explicitFeature is not null
            ? Humanizer.Identifier(explicitFeature)
            : featureParts.Count > 0
                ? string.Join(" · ", featureParts)
                : subject;
        return new(module, feature, subject);
    }
}
