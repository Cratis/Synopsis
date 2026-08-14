// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Cratis.Synopsis.Discovery;

internal static partial class Humanizer
{
    public static string Identifier(string value, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..];
            }
        }

        value = value.Replace('_', ' ').Replace('-', ' ');
        value = AcronymBoundary().Replace(value, "$1 $2");
        value = WordBoundary().Replace(value, "$1 $2");
        value = Whitespace().Replace(value, " ").Trim();
        return value.Length == 0 ? "System" : char.ToUpperInvariant(value[0]) + value[1..];
    }

    public static string Outcome(string value) => Identifier(value, "should_", "it_");

    [GeneratedRegex("([A-Z]+)([A-Z][a-z])")]
    private static partial Regex AcronymBoundary();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundary();

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();
}
