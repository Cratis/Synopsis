// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Specs.for_SpecificationDiscoverer;

public class when_discovering_a_screenplay_specification : given.a_repository
{
    BehaviorScenario _scenario;

    void Because()
    {
        Write("bookshop.play", """
            module Orders
              feature Checkout
                slice StateChange PlaceOrder
                  specification PlacingAValidOrder
                    given CustomerRegistered
                    when PlaceOrder
                    then OrderPlaced
            """);
        Discover();
        _scenario = _result.Scenarios.Single();
    }

    [Fact] void should_use_the_declared_module() => _scenario.Module.ShouldEqual("Orders");
    [Fact] void should_use_the_declared_feature() => _scenario.Feature.ShouldEqual("Checkout");
    [Fact] void should_capture_the_given_event() => _scenario.Given.Single().Text.ShouldEqual("Customer Registered");
    [Fact] void should_capture_the_command() => _scenario.When.Text.ShouldEqual("Place Order");
    [Fact] void should_capture_the_then_event() => _scenario.Then.Single().Text.ShouldEqual("Order Placed");
    [Fact] void should_identify_it_as_model_behavior() => _scenario.Surface.ShouldEqual("Model");
}
