// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Model;

/// <summary>
/// Identifies where a piece of behavior is specified.
/// </summary>
/// <param name="Path">Repository-relative source path.</param>
/// <param name="Line">One-based source line.</param>
/// <param name="Url">Optional browser URL for the source line.</param>
public record SourceLocation(string Path, int Line, string? Url = null);
