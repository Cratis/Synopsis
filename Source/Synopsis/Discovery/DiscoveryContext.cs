// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Discovery;

internal class DiscoveryContext
{
    public DiscoveryContext(string root, DiscoveryOptions options)
    {
        Root = root;
        Options = options;
    }

    public string Root { get; }

    public DiscoveryOptions Options { get; }

    public PathClassification Classify(string relativePath, string? explicitModule = null, string? explicitFeature = null) =>
        PathClassifier.Classify(relativePath, Options.SkipSegments, explicitModule, explicitFeature);

    public SourceLocation Locate(string path, int line)
    {
        var baseUrl = Options.SourceUrl?.TrimEnd('/');
        var url = baseUrl is null ? null : $"{baseUrl}/blob/main/{path}#L{line}";
        return new(path, line, url);
    }
}
