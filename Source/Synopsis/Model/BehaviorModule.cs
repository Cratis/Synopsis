// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Model;

/// <summary>
/// Groups features into a domain module.
/// </summary>
/// <param name="Name">Module name.</param>
/// <param name="Features">Module features.</param>
public record BehaviorModule(string Name, IReadOnlyList<BehaviorFeature> Features);
