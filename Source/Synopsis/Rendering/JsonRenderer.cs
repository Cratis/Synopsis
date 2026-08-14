// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Rendering;

/// <summary>
/// Renders the stable, language-neutral Synopsis interchange format.
/// </summary>
public class JsonRenderer
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes a behavior document.
    /// </summary>
    /// <param name="document">Document to serialize.</param>
    /// <returns>Formatted JSON.</returns>
    public string Render(SynopsisDocument document) => JsonSerializer.Serialize(document, Options);
}
