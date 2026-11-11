// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Specs.for_ProjectConfiguration;

public class when_inferring_from_an_http_remote : given.a_git_repository
{
    void Establish() => _input = GivenRemote("http://example-user@example.com:8080/owner/repo");

    void Because() => _result = ProjectConfiguration.InferGitHubUrl(_input);

    [Fact] void should_preserve_the_scheme_host_port_and_path() => _result.ShouldEqual("http://example.com:8080/owner/repo");
}
