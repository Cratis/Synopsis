// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Synopsis.Model;
using Cratis.Synopsis.Rendering;

namespace Cratis.Synopsis.Specs.for_HtmlRenderer;

public class when_rendering_a_document : Specification
{
    string _result;

    void Because()
    {
        var scenario = new BehaviorScenario("id", "Orders", "Checkout", "Cart", "Checking out", [new("A cart with an item")], new("The customer checks out"), [new("A receipt is issued")], "C#", "Backend", new("spec.cs", 12));
        var document = new SynopsisDocument("1.0", "Bookshop", "Its promises", ".", null, [new("Orders", [new("Checkout", [scenario])])], []);
        _result = new HtmlRenderer().Render(document);
    }

    [Fact] void should_render_a_complete_document() => _result.StartsWith("<!doctype html>", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_render_the_behavior_story() => _result.ShouldContain("A receipt is issued");
    [Fact] void should_include_search() => _result.ShouldContain("id=\"search\"");
    [Fact] void should_include_print_styles() => _result.ShouldContain("@media print");
    [Fact] void should_not_depend_on_external_assets() => _result.ShouldNotContain("<link");
}
