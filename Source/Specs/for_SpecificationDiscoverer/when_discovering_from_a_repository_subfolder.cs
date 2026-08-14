// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Specs.for_SpecificationDiscoverer;

public class when_discovering_from_a_repository_subfolder : given.a_repository
{
    BehaviorScenario _scenario;

    void Because()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        Write("Samples/Bookshop/Source/Catalog/Search/for_catalogSearch/when_searching.ts", """
            describe('when searching the catalog', () => {
                it('shows matching books', () => results.should.not.be.empty);
            });
            """);
        Discover("Samples/Bookshop");
        _scenario = _result.Scenarios.Single();
    }

    [Fact] void should_infer_the_module_relative_to_the_selected_input() => _scenario.Module.ShouldEqual("Catalog");
    [Fact] void should_keep_the_source_path_relative_to_the_repository() => _scenario.Source.Path.ShouldEqual("Samples/Bookshop/Source/Catalog/Search/for_catalogSearch/when_searching.ts");
}
