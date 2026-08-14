// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Specs.for_SpecificationDiscoverer;

public class when_discovering_a_csharp_specification : given.a_repository
{
    BehaviorScenario _scenario;

    void Because()
    {
        Write("Source/Orders/Checkout/for_cart/when_checking_out.cs", """
            #if DEBUG
            class when_checking_out : given.a_cart_with_an_item
            {
                Receipt _receipt;
                void Establish() => customer.SignIn();
                void Because() => _receipt = cart.Checkout();
                [Fact] void should_issue_a_receipt() => _receipt.ShouldNotBeNull();
                [Fact] void should_empty_the_cart() => cart.Items.ShouldBeEmpty();
            }

            class a_cart_with_an_item : Specification
            {
                void Establish() => cart.Add(anItem);
            }
            #endif
            """);
        Discover();
        _scenario = _result.Scenarios.Single();
    }

    [Fact] void should_find_the_scenario() => _scenario.Title.ShouldEqual("Checking out");
    [Fact] void should_infer_the_module() => _scenario.Module.ShouldEqual("Orders");
    [Fact] void should_infer_the_feature() => _scenario.Feature.ShouldEqual("Checkout");
    [Fact] void should_name_the_subject() => _scenario.Subject.ShouldEqual("Cart");
    [Fact] void should_include_the_inherited_context() => _scenario.Given.Any(_ => _.Text == "A cart with an item").ShouldBeTrue();
    [Fact] void should_include_the_local_context() => _scenario.Given.Any(_ => _.Text == "The scenario context").ShouldBeTrue();
    [Fact] void should_capture_the_trigger_as_evidence() => _scenario.When.Details!.ShouldContain("cart.Checkout()");
    [Fact] void should_capture_every_outcome() => _scenario.Then.Select(_ => _.Text).ShouldContainOnly("Issue a receipt", "Empty the cart");
    [Fact] void should_identify_it_as_backend_behavior() => _scenario.Surface.ShouldEqual("Backend");
}
