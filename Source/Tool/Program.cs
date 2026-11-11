// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Discovery;

if (args.Contains("--help") || args.Contains("-h"))
{
    Help.Print();
    return 0;
}

if (args.Contains("--version"))
{
    Console.WriteLine(typeof(SpecificationDiscoverer).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");
    return 0;
}

return Cli.Run(args);
