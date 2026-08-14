// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Model;

/// <summary>
/// Describes non-fatal input Synopsis could not interpret completely.
/// </summary>
/// <param name="Path">Source path.</param>
/// <param name="Message">Readable diagnostic.</param>
public record DiscoveryDiagnostic(string Path, string Message);
