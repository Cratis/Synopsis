// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Model;

/// <summary>
/// Represents one readable part of a Given / When / Then behavior.
/// </summary>
/// <param name="Text">Human-readable behavior text.</param>
/// <param name="Details">Optional implementation excerpt that supplies evidence.</param>
public record BehaviorStep(string Text, string? Details = null);
