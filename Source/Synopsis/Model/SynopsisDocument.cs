// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Model;

/// <summary>
/// Represents the complete, language-neutral behavioral account of a repository.
/// </summary>
/// <param name="SchemaVersion">Version of the serialized model contract.</param>
/// <param name="Title">Document title.</param>
/// <param name="Description">Document description.</param>
/// <param name="SourceRoot">Analyzed repository root.</param>
/// <param name="SourceUrl">Optional repository browser URL.</param>
/// <param name="Modules">Discovered behavior grouped by module and feature.</param>
/// <param name="Diagnostics">Non-fatal discovery diagnostics.</param>
public record SynopsisDocument(
    string SchemaVersion,
    string Title,
    string Description,
    string SourceRoot,
    string? SourceUrl,
    IReadOnlyList<BehaviorModule> Modules,
    IReadOnlyList<DiscoveryDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets all scenarios in the document.
    /// </summary>
    public IEnumerable<BehaviorScenario> Scenarios => Modules.SelectMany(_ => _.Features).SelectMany(_ => _.Scenarios);
}
