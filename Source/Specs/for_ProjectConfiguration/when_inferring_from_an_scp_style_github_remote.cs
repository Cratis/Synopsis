// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Specs.for_ProjectConfiguration;

public class when_inferring_from_an_scp_style_github_remote : given.a_git_repository
{
    void Establish() => _input = GivenRemote("git@github.com:owner/repo.git");

    void Because() => _result = ProjectConfiguration.InferGitHubUrl(_input);

    [Fact] void should_infer_an_https_repository_url() => _result.ShouldEqual("https://github.com/owner/repo");
}
