// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Specs.for_SpecificationDiscoverer;

public class when_discovering_plain_gherkin_features : given.a_repository
{
    IReadOnlyList<BehaviorScenario> _scenarios;

    void Because()
    {
        Write("Features/Orders/cancel.feature", """
            # A comment before the feature
            @orders
            Feature: Order cancellation

              Scenario: Cancelling an order
                Given a placed order
                When the customer cancels it
                Then the order is cancelled
                But no refund is issued
            """);
        Write("Features/Orders/empty.feature", """
            Feature: Nothing here yet
            """);
        Discover();
        _scenarios = _result.Scenarios.ToList();
    }

    [Fact] void should_discover_the_scenario() => _scenarios.Single().Title.ShouldEqual("Cancelling an order");
    [Fact] void should_use_the_feature_declaration_and_file_name_subject() => (_scenarios.Single().Feature == "Order cancellation" && _scenarios.Single().Subject == "Cancel").ShouldBeTrue();
    [Fact] void should_keep_the_given_and_then_steps() => (_scenarios.Single().Given.Single().Text == "a placed order" && _scenarios.Single().Then.Count == 2).ShouldBeTrue();
    [Fact] void should_use_the_when_step_as_the_trigger() => _scenarios.Single().When.Text.ShouldEqual("the customer cancels it");
    [Fact] void should_report_a_feature_file_without_scenarios() => _result.Diagnostics.Single().Message.ShouldEqual("The feature file contains no scenarios.");
}
