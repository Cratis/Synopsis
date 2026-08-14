// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Discovery;

/// <summary>
/// Controls repository specification discovery.
/// </summary>
public record DiscoveryOptions
{
    /// <summary>
    /// Gets the input repository or source directory.
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// Gets the title used in rendered output.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the short explanation printed below the title.
    /// </summary>
    public string Description { get; init; } = "A living account of the behavior this system specifies.";

    /// <summary>
    /// Gets an optional repository URL used for source links.
    /// </summary>
    public string? SourceUrl { get; init; }

    /// <summary>
    /// Gets path segments ignored before module inference.
    /// </summary>
    public IReadOnlyList<string> SkipSegments { get; init; } = [];

    /// <summary>
    /// Gets additional directory names excluded from discovery.
    /// </summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];
}
