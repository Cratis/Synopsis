// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Discovery;

internal interface ISpecificationParser
{
    IReadOnlySet<string> Extensions { get; }

    ParseResult Parse(IReadOnlyList<SourceFile> files, DiscoveryContext context);
}
