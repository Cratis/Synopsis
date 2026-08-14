// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Discovery;

internal record ParseResult(IReadOnlyList<BehaviorScenario> Scenarios, IReadOnlyList<DiscoveryDiagnostic> Diagnostics)
{
    public static readonly ParseResult Empty = new([], []);
}
