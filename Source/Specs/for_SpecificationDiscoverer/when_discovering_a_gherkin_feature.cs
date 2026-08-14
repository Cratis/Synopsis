// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Specs.for_SpecificationDiscoverer;

public class when_discovering_a_gherkin_feature : given.a_repository
{
    IReadOnlyList<BehaviorScenario> _scenarios;

    void Because()
    {
        Write("Features/Orders/refunds.feature", """
            Feature: Order checkout

              Background:
                Given a signed-in customer

              Rule: Refunds

                Scenario Outline: Refunding a <kind> payment
                  Given a completed <kind> payment
                  When the customer requests a refund
                  And the payment provider accepts it
                  Then the money is returned

                  Examples:
                    | kind   |
                    | card   |
                    | wallet |
            """);
        Discover();
        _scenarios = _result.Scenarios.ToList();
    }

    [Fact] void should_expand_scenario_outline_examples() => _scenarios.Select(_ => _.Title).ShouldContainOnly("Refunding a card payment", "Refunding a wallet payment");
    [Fact] void should_derive_the_module_feature_and_rule_subject() => _scenarios.All(_ => _.Module == "Orders" && _.Feature == "Order checkout · Refunds" && _.Subject == "Refunds").ShouldBeTrue();
    [Fact] void should_apply_the_background_and_example_values() => _scenarios.All(_ => _.Given.Any(step => step.Text == "a signed-in customer") && _.Given.Any(step => step.Text.StartsWith("a completed"))).ShouldBeTrue();
    [Fact] void should_keep_chained_when_steps_as_the_trigger() => _scenarios[0].When.Text.ShouldEqual("the customer requests a refund and the payment provider accepts it");
    [Fact] void should_identify_gherkin_as_model_behavior() => _scenarios.All(_ => _.Language == "Gherkin" && _.Surface == "Model").ShouldBeTrue();
}
