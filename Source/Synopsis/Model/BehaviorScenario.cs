// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Model;

/// <summary>
/// Represents an executable example of system behavior.
/// </summary>
/// <param name="Id">Stable identifier derived from the source location.</param>
/// <param name="Module">Owning domain module.</param>
/// <param name="Feature">Owning feature.</param>
/// <param name="Subject">Unit, slice, or capability under specification.</param>
/// <param name="Title">Readable scenario title.</param>
/// <param name="Given">Preconditions and shared contexts.</param>
/// <param name="When">The behavior being exercised.</param>
/// <param name="Then">Observable outcomes.</param>
/// <param name="Language">Input specification language.</param>
/// <param name="Surface">Backend, frontend, or model surface.</param>
/// <param name="Source">Source location.</param>
public record BehaviorScenario(
    string Id,
    string Module,
    string Feature,
    string Subject,
    string Title,
    IReadOnlyList<BehaviorStep> Given,
    BehaviorStep When,
    IReadOnlyList<BehaviorStep> Then,
    string Language,
    string Surface,
    SourceLocation Source);
