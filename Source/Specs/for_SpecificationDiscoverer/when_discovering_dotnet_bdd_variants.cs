// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Specs.for_SpecificationDiscoverer;

public class when_discovering_dotnet_bdd_variants : given.a_repository
{
    IReadOnlyList<BehaviorScenario> _scenarios;

    void Because()
    {
        Write("for_PaymentProcessor/payment_specs.cs", """
            namespace Cratis.Payments.Checkout.for_PaymentProcessor;

            record CheckoutTests : given_a_registered_customer, IClassFixture<CardFixture>
            {
                [SetUp] void Arrange() => card = AnExpiredCard();
                void Act() => result = processor.Charge(card);

                [TestCase(42, TestName = "Declines an expired card")]
                void returns_a_decline(int amount) => result.ShouldBeDeclined();
            }

            class when_refunding : Specification
            {
                Establish context = () => payment = ACompletedPayment();
                Because of = () => result = payment.Refund();
                It should_return_the_money = () => result.ShouldBeRefunded();
                It should_record_the_refund = () => payment.Refunds.ShouldNotBeEmpty();
            }
            """);
        Discover();
        _scenarios = _result.Scenarios.ToList();
    }

    [Fact] void should_discover_attribute_and_delegate_based_specs() => _scenarios.Count.ShouldEqual(2);
    [Fact] void should_use_the_namespace_when_the_path_only_contains_behavior_folders() => _scenarios.All(_ => _.Module == "Payments" && _.Feature == "Checkout").ShouldBeTrue();
    [Fact] void should_remove_test_suffixes_from_scenario_names() => _scenarios.Single(_ => _.Title == "Checkout").ShouldNotBeNull();
    [Fact] void should_use_framework_display_names() => _scenarios.Single(_ => _.Title == "Checkout").Then.Single().Text.ShouldEqual("Declines an expired card");
    [Fact] void should_not_present_test_runner_fixture_interfaces_as_business_context() => _scenarios.Single(_ => _.Title == "Checkout").Given.All(_ => _.Text != "I Class Fixture").ShouldBeTrue();
    [Fact] void should_understand_mspec_context_act_and_assertion_fields() => _scenarios.Single(_ => _.Title == "Refunding").Then.Count.ShouldEqual(2);
}
