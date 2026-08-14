// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Specs.for_SpecificationDiscoverer;

public class when_discovering_a_typescript_specification : given.a_repository
{
    BehaviorScenario _scenario;

    void Because()
    {
        Write("Source/Orders/Checkout/for_checkoutButton/when_clicked.tsx", """
            describe('when the checkout button is clicked', () => {
                beforeEach(() => page.render(aCart));

                it('should show the confirmation', () => {
                    page.confirmation.should.exist;
                });

                it('should disable another submission', () => {
                    page.button.disabled.should.equal(true);
                });
            });
            """);
        Discover();
        _scenario = _result.Scenarios.Single();
    }

    [Fact] void should_find_the_scenario() => _scenario.Title.ShouldEqual("The checkout button is clicked");
    [Fact] void should_keep_the_readable_outcomes() => _scenario.Then.Select(_ => _.Text).ShouldContainOnly("should show the confirmation", "should disable another submission");
    [Fact] void should_capture_before_each_as_context() => _scenario.Given.Single().Details!.ShouldContain("page.render(aCart)");
    [Fact] void should_identify_it_as_frontend_behavior() => _scenario.Surface.ShouldEqual("Frontend");
}
