// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Specs.for_SpecificationDiscoverer;

public class when_discovering_screenplay_error_and_unnamed_trigger_specifications : given.a_repository
{
    IReadOnlyList<BehaviorScenario> _scenarios;

    void Because()
    {
        Write("shop.play", "module Orders\r\n  feature Checkout\r\n    slice StateChange PlaceOrder\r\n      specification RejectingAnEmptyCart\r\n        given CustomerRegistered\r\n        when PlaceOrder { items: [] }\r\n\r\n        then error \"The cart is empty\"\r\n      specification BareError\r\n        then error\r\n      specification NothingElse\r\n        then OrderPlaced\r\n");
        Discover();
        _scenarios = _result.Scenarios.ToList();
    }

    [Fact] void should_discover_every_specification() => _scenarios.Count.ShouldEqual(3);
    [Fact] void should_describe_the_error_reason() => _scenarios.Single(_ => _.Title == "Rejecting An Empty Cart").Then.Single().Text.ShouldEqual("The error is ‘The cart is empty’");
    [Fact] void should_describe_an_error_without_a_reason() => _scenarios.Single(_ => _.Title == "Bare Error").Then.Single().Text.ShouldEqual("An error is returned");
    [Fact] void should_stop_the_trigger_name_at_its_payload() => _scenarios.Single(_ => _.Title == "Rejecting An Empty Cart").When.Text.ShouldEqual("Place Order");
    [Fact] void should_use_the_title_as_trigger_when_there_is_no_when_statement() => _scenarios.Single(_ => _.Title == "Nothing Else").When.Text.ShouldEqual("Nothing Else");
}
