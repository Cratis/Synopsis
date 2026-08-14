// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Specs.for_SpecificationDiscoverer;

public class when_discovering_javascript_bdd_variants : given.a_repository
{
    IReadOnlyList<BehaviorScenario> _scenarios;

    void Because()
    {
        Write("Source/Orders/Checkout/checkout.spec.ts", """
            context.only('given a signed-in customer', function () {
                before(async function () { await signIn(); });

                describe.concurrent('when checking out', () => {
                    beforeEach(async () => renderCart());
                    const commandPattern = /charge\((?<amount>\d+)\)/;

                    test.each([[10], [20]])('charges %s', async amount => charge(amount));
                    it.todo('sends a receipt');
                });
            });

            // Documentation can mention it('without becoming a test', () => fail()).
            specify('loads without a cart', function () { expect(cart).toBeUndefined(); });

            test.describe('when using Playwright', () => {
                test.beforeEach(async ({ page }) => page.goto('/'));
                test('shows the application', async ({ page }) => expect(page).toBeVisible());
            });

            xdescribe('when temporarily disabled', () => {
                fit('still remains documented', () => true);
            });
            """);
        Write("Source/Billing/for_invoice/malformed.spec.ts", """
            describe('when an unfinished suite is being edited', () => {
                it('still has an open block', () => true);
            """);
        Discover();
        _scenarios = _result.Scenarios.ToList();
    }

    [Fact] void should_create_nested_and_top_level_scenarios() => _scenarios.Count.ShouldEqual(5);
    [Fact] void should_turn_the_outer_suite_into_context() => _scenarios.Single(_ => _.Title == "Checking out").Given.Any(_ => _.Text == "A signed in customer").ShouldBeTrue();
    [Fact] void should_include_ancestor_and_local_hooks() => _scenarios.Single(_ => _.Title == "Checking out").Given.Count(_ => _.Details is not null).ShouldEqual(2);
    [Fact] void should_discover_data_driven_and_todo_tests() => _scenarios.Single(_ => _.Title == "Checking out").Then.Select(_ => _.Text).ShouldContainOnly("charges %s", "sends a receipt");
    [Fact] void should_derive_a_clean_title_for_top_level_tests() => _scenarios.Single(_ => _.Title == "Checkout").Then.Single().Text.ShouldEqual("loads without a cart");
    [Fact] void should_understand_playwright_qualified_suites_and_hooks() => _scenarios.Single(_ => _.Title == "Using Playwright").Given.Single().Details!.ShouldContain("page.goto");
    [Fact] void should_keep_jasmine_focused_and_disabled_behavior_visible() => _scenarios.Single(_ => _.Title == "Temporarily disabled").Then.Single().Text.ShouldEqual("still remains documented");
    [Fact] void should_report_malformed_bdd_without_hiding_other_behavior() => _result.Diagnostics.Single().Message.ShouldContain("describe suite");
    [Fact] void should_recover_the_readable_part_of_an_unfinished_suite() => _scenarios.Single(_ => _.Title == "An unfinished suite is being edited").Then.Single().Text.ShouldEqual("still has an open block");
}
