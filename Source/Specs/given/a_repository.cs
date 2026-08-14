// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Discovery;
using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Specs.given;

public class a_repository : Specification
{
    protected string _root;
    protected SynopsisDocument _result;

    void Establish() => _root = Path.Combine(Path.GetTempPath(), $"synopsis-spec-{Guid.NewGuid():N}");

    protected void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    protected void Discover() => _result = new SpecificationDiscoverer().Discover(new()
    {
        Input = _root,
        Title = "Example"
    });

    void Destroy()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
