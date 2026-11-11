// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Synopsis.Specs.for_ProjectConfiguration;

public class when_inferring_from_an_https_remote_with_query_and_fragment : given.a_git_repository
{
    void Establish() => _input = GivenRemote("https://github.com/owner/repo.git?access=value#section");

    void Because() => _result = ProjectConfiguration.InferGitHubUrl(_input);

    [Fact] void should_infer_the_repository_url_without_query_or_fragment() => _result.ShouldEqual("https://github.com/owner/repo");
}
