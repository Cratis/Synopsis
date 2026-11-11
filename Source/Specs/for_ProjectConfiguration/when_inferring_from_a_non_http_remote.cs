// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Specs.for_ProjectConfiguration;

public class when_inferring_from_a_non_http_remote : given.a_git_repository
{
    string[] _inputs;
    string?[] _results;

    void Establish() => _inputs = new[]
    {
        "ssh://git@github.com/owner/repo.git",
        "file:///example/repo.git",
        "/example/repo.git",
        "ftp://example.com/owner/repo.git",
        "https://[invalid"
    }.Select(GivenRemote).ToArray();

    void Because() => _results = _inputs.Select(ProjectConfiguration.InferGitHubUrl).ToArray();

    [Fact] void should_not_infer_an_ssh_repository_url() => _results[0].ShouldBeNull();
    [Fact] void should_not_infer_a_file_repository_url() => _results[1].ShouldBeNull();
    [Fact] void should_not_infer_a_local_repository_url() => _results[2].ShouldBeNull();
    [Fact] void should_not_infer_an_ftp_repository_url() => _results[3].ShouldBeNull();
    [Fact] void should_not_infer_an_unparsable_repository_url() => _results[4].ShouldBeNull();
}
