// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Specs.for_ProjectConfiguration.given;

public class a_git_repository : Specification
{
    string _root;
    protected string _input;
    protected string? _result;

    void Establish() => _root = Path.Combine(Path.GetTempPath(), $"synopsis-remote-spec-{Guid.NewGuid():N}");

    protected string GivenRemote(string remote)
    {
        var input = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var git = Path.Combine(input, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllText(Path.Combine(git, "config"), $"[remote \"origin\"]\n    url = {remote}\n");
        return input;
    }

    void Destroy() => Directory.Delete(_root, true);
}
