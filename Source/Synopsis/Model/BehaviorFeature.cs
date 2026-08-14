// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Model;

/// <summary>
/// Groups scenarios belonging to one feature.
/// </summary>
/// <param name="Name">Feature name.</param>
/// <param name="Scenarios">Feature scenarios.</param>
public record BehaviorFeature(string Name, IReadOnlyList<BehaviorScenario> Scenarios);
