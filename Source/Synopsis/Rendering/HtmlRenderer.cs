// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using Cratis.Synopsis.Model;

namespace Cratis.Synopsis.Rendering;

/// <summary>
/// Renders a self-contained, searchable behavior site.
/// </summary>
public class HtmlRenderer
{
    /// <summary>
    /// Renders the behavior document as one portable HTML file with no external dependencies.
    /// </summary>
    /// <param name="document">Document to render.</param>
    /// <returns>Complete HTML.</returns>
    public string Render(SynopsisDocument document)
    {
        var scenarios = document.Scenarios.ToList();
        var content = new StringBuilder();
        foreach (var module in document.Modules)
        {
            RenderModule(content, module);
        }

        var navigation = string.Join("", document.Modules.Select(module =>
            $"<a href=\"#{Slug(module.Name)}\"><span>{Encode(module.Name)}</span><b>{module.Features.Sum(_ => _.Scenarios.Count)}</b></a>"));
        var diagnostics = RenderDiagnostics(document.Diagnostics);
        return Template
            .Replace("{{TITLE}}", Encode(document.Title), StringComparison.Ordinal)
            .Replace("{{DESCRIPTION}}", Encode(document.Description), StringComparison.Ordinal)
            .Replace("{{MODULES}}", document.Modules.Count.ToString(), StringComparison.Ordinal)
            .Replace("{{FEATURES}}", document.Modules.Sum(_ => _.Features.Count).ToString(), StringComparison.Ordinal)
            .Replace("{{SCENARIOS}}", scenarios.Count.ToString(), StringComparison.Ordinal)
            .Replace("{{OUTCOMES}}", scenarios.Sum(_ => _.Then.Count).ToString(), StringComparison.Ordinal)
            .Replace("{{BACKEND}}", scenarios.Count(_ => _.Surface == "Backend").ToString(), StringComparison.Ordinal)
            .Replace("{{FRONTEND}}", scenarios.Count(_ => _.Surface == "Frontend").ToString(), StringComparison.Ordinal)
            .Replace("{{MODEL}}", scenarios.Count(_ => _.Surface == "Model").ToString(), StringComparison.Ordinal)
            .Replace("{{NAVIGATION}}", navigation, StringComparison.Ordinal)
            .Replace("{{CONTENT}}", content.ToString(), StringComparison.Ordinal)
            .Replace("{{DIAGNOSTICS}}", diagnostics, StringComparison.Ordinal);
    }

    static void RenderModule(StringBuilder output, BehaviorModule module)
    {
        output.Append($"<section class=\"module\" id=\"{Slug(module.Name)}\"><header class=\"module-head\"><div><span class=\"eyebrow\">Module</span><h2>{Encode(module.Name)}</h2></div><span class=\"module-count\">{module.Features.Sum(_ => _.Scenarios.Count)} behaviors</span></header>");
        foreach (var feature in module.Features)
        {
            output.Append($"<section class=\"feature\"><div class=\"feature-head\"><h3>{Encode(feature.Name)}</h3><span>{feature.Scenarios.Count} scenarios</span></div><div class=\"scenario-grid\">");
            foreach (var scenario in feature.Scenarios)
            {
                RenderScenario(output, scenario);
            }
            output.Append("</div></section>");
        }
        output.Append("</section>");
    }

    static void RenderScenario(StringBuilder output, BehaviorScenario scenario)
    {
        var search = Encode(string.Join(' ', new[] { scenario.Module, scenario.Feature, scenario.Subject, scenario.Title, scenario.Language, scenario.Surface }.Concat(scenario.Given.Select(_ => _.Text)).Concat(scenario.Then.Select(_ => _.Text))).ToLowerInvariant());
        output.Append($"<article class=\"scenario\" data-search=\"{search}\" data-surface=\"{scenario.Surface.ToLowerInvariant()}\">");
        output.Append("<header class=\"scenario-head\"><div class=\"scenario-title\">");
        output.Append($"<span class=\"subject\">For {Encode(scenario.Subject)}</span><h4>{Encode(scenario.Title)}</h4></div>");
        output.Append($"<div class=\"tags\"><span class=\"tag surface-{scenario.Surface.ToLowerInvariant()}\">{Encode(scenario.Surface)}</span><span class=\"tag\">{Encode(scenario.Language)}</span></div></header>");
        output.Append("<div class=\"story\">");
        RenderLane(output, "Given", scenario.Given.Count == 0 ? [new("No additional precondition")] : scenario.Given, "given");
        RenderLane(output, "When", [scenario.When], "when");
        RenderLane(output, "Then", scenario.Then.Count == 0 ? [new("The specified behavior completes")] : scenario.Then, "then");
        output.Append("</div><footer class=\"source\">");
        var label = $"{scenario.Source.Path}:{scenario.Source.Line}";
        output.Append(scenario.Source.Url is null
            ? $"<span title=\"Source specification\">↗ {Encode(label)}</span>"
            : $"<a href=\"{Encode(scenario.Source.Url)}\" target=\"_blank\" rel=\"noreferrer\">↗ {Encode(label)}</a>");
        output.Append("</footer></article>");
    }

    static void RenderLane(StringBuilder output, string label, IReadOnlyList<BehaviorStep> steps, string kind)
    {
        output.Append($"<div class=\"lane lane-{kind}\"><div class=\"lane-label\"><i></i>{label}</div><div class=\"steps\">");
        foreach (var step in steps)
        {
            output.Append($"<div class=\"step\"><span>{Encode(step.Text)}</span>");
            if (!string.IsNullOrWhiteSpace(step.Details))
            {
                output.Append($"<details><summary>Evidence in code</summary><pre><code>{Encode(step.Details!)}</code></pre></details>");
            }
            output.Append("</div>");
        }
        output.Append("</div></div>");
    }

    static string RenderDiagnostics(IReadOnlyList<DiscoveryDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return string.Empty;
        }

        var items = string.Join("", diagnostics.Select(_ => $"<li><code>{Encode(_.Path)}</code> — {Encode(_.Message)}</li>"));
        return $"<details class=\"diagnostics\"><summary>{diagnostics.Count} discovery notes</summary><ul>{items}</ul></details>";
    }

    static string Encode(string value) => WebUtility.HtmlEncode(value);

    static string Slug(string value)
    {
        var slug = new string(value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }
        return slug.Trim('-');
    }

    const string Template = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="color-scheme" content="light dark">
<title>{{TITLE}}</title>
<style>
:root{--paper:#f6f2e9;--paper-2:#fffdf7;--ink:#17202a;--muted:#68706f;--line:#dcd6c9;--navy:#18324a;--navy-2:#244863;--coral:#e4664e;--gold:#dda93a;--green:#2f7b67;--violet:#755f9c;--shadow:0 18px 55px rgba(35,43,46,.09);--radius:18px}
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:var(--paper);color:var(--ink);font:15px/1.55 Inter,ui-sans-serif,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}button,input{font:inherit}a{color:inherit}.shell{display:grid;grid-template-columns:250px minmax(0,1fr);min-height:100vh}.sidebar{position:sticky;top:0;height:100vh;padding:30px 22px;background:var(--navy);color:#eef5f2;overflow:auto}.brand{display:flex;align-items:center;gap:11px;margin-bottom:34px;font-weight:760;font-size:18px;letter-spacing:-.02em}.mark{display:grid;place-items:center;width:36px;height:36px;border-radius:11px;background:var(--coral);color:white;box-shadow:0 8px 24px #0d1a2677}.nav-label{margin:0 8px 9px;color:#91a7b5;text-transform:uppercase;font-size:10px;font-weight:800;letter-spacing:.16em}.sidebar nav{display:grid;gap:4px}.sidebar nav a{display:flex;align-items:center;justify-content:space-between;padding:9px 10px;border-radius:9px;color:#d9e4e7;text-decoration:none}.sidebar nav a:hover{background:#ffffff12}.sidebar nav b{font-size:11px;color:#8fa8b6}.side-note{margin-top:34px;padding:14px;border:1px solid #ffffff16;border-radius:12px;color:#a9bdc8;font-size:12px}.page{min-width:0}.hero{padding:72px clamp(28px,6vw,92px) 58px;background:linear-gradient(135deg,var(--navy) 0%,var(--navy-2) 65%,#31576c 100%);color:white;overflow:hidden;position:relative}.hero:after{content:"";position:absolute;right:-120px;top:-190px;width:470px;height:470px;border:1px solid #ffffff18;border-radius:50%;box-shadow:0 0 0 70px #ffffff08,0 0 0 140px #ffffff05}.kicker{position:relative;z-index:1;color:#ffc7b9;text-transform:uppercase;font-size:11px;font-weight:850;letter-spacing:.18em}.hero h1{position:relative;z-index:1;max-width:850px;margin:12px 0 12px;font-family:Georgia,"Times New Roman",serif;font-size:clamp(44px,7vw,82px);font-weight:500;line-height:.98;letter-spacing:-.045em}.hero>p{position:relative;z-index:1;max-width:700px;margin:0;color:#cedce2;font-size:18px}.metrics{position:relative;z-index:1;display:grid;grid-template-columns:repeat(4,minmax(100px,1fr));max-width:850px;margin-top:42px;border:1px solid #ffffff1c;border-radius:14px;background:#0b213252;backdrop-filter:blur(10px)}.metric{padding:18px 20px;border-right:1px solid #ffffff1c}.metric:last-child{border:0}.metric b{display:block;font:500 30px/1 Georgia,serif}.metric span{color:#9fb4c0;font-size:11px;text-transform:uppercase;letter-spacing:.1em}.toolbar{position:sticky;top:0;z-index:10;display:flex;gap:12px;align-items:center;padding:14px clamp(24px,5vw,72px);background:#f6f2e9e8;border-bottom:1px solid var(--line);backdrop-filter:blur(14px)}.search{position:relative;flex:1}.search span{position:absolute;left:15px;top:10px;color:var(--muted)}.search input{width:100%;padding:10px 14px 10px 42px;border:1px solid var(--line);border-radius:12px;background:var(--paper-2);color:var(--ink);outline:none}.search input:focus{border-color:var(--coral);box-shadow:0 0 0 3px #e4664e1f}.filter{display:flex;gap:5px}.filter button,.print{padding:9px 12px;border:1px solid var(--line);border-radius:10px;background:var(--paper-2);color:var(--muted);cursor:pointer}.filter button.active{background:var(--navy);border-color:var(--navy);color:white}.main{max-width:1240px;margin:auto;padding:48px clamp(24px,5vw,72px) 100px}.legend{display:flex;align-items:center;justify-content:space-between;gap:20px;margin-bottom:52px;padding:20px 22px;background:var(--paper-2);border:1px solid var(--line);border-radius:var(--radius)}.legend p{margin:0;color:var(--muted)}.surface-totals{display:flex;gap:16px;white-space:nowrap}.surface-totals span{font-size:12px;font-weight:750}.surface-totals i{display:inline-block;width:8px;height:8px;margin-right:6px;border-radius:50%}.back{background:var(--green)}.front{background:var(--violet)}.model{background:var(--gold)}.module{scroll-margin-top:80px;margin:0 0 68px}.module-head{display:flex;justify-content:space-between;align-items:end;margin-bottom:26px;padding-bottom:15px;border-bottom:2px solid var(--ink)}.eyebrow{color:var(--coral);text-transform:uppercase;font-size:10px;font-weight:850;letter-spacing:.16em}.module h2{margin:2px 0 0;font:500 37px/1.1 Georgia,serif;letter-spacing:-.025em}.module-count,.feature-head span{color:var(--muted);font-size:12px}.feature{margin:0 0 42px}.feature-head{display:flex;align-items:center;justify-content:space-between;margin-bottom:13px}.feature h3{margin:0;font-size:14px;text-transform:uppercase;letter-spacing:.08em}.scenario-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px}.scenario{display:flex;flex-direction:column;min-width:0;background:var(--paper-2);border:1px solid var(--line);border-radius:var(--radius);box-shadow:0 3px 18px rgba(27,39,46,.035);overflow:hidden;transition:transform .18s,box-shadow .18s}.scenario:hover{transform:translateY(-2px);box-shadow:var(--shadow)}.scenario-head{display:flex;justify-content:space-between;gap:16px;padding:20px 22px 17px;border-bottom:1px solid var(--line)}.subject{color:var(--muted);font-size:11px}.scenario h4{margin:3px 0 0;font:500 20px/1.2 Georgia,serif}.tags{display:flex;gap:5px;align-items:start}.tag{padding:3px 7px;border:1px solid var(--line);border-radius:99px;color:var(--muted);font-size:9px;font-weight:800;text-transform:uppercase;letter-spacing:.07em}.surface-backend{color:var(--green);border-color:#2f7b6745;background:#2f7b670d}.surface-frontend{color:var(--violet);border-color:#755f9c45;background:#755f9c0d}.surface-model{color:#9a6f12;border-color:#dda93a66;background:#dda93a12}.story{padding:5px 22px 12px}.lane{display:grid;grid-template-columns:65px 1fr;gap:10px;padding:13px 0;border-bottom:1px solid #e9e4da}.lane:last-child{border:0}.lane-label{padding-top:2px;font-size:10px;font-weight:850;text-transform:uppercase;letter-spacing:.1em}.lane-label i{display:inline-block;width:7px;height:7px;margin-right:7px;border-radius:50%}.lane-given i{background:var(--gold)}.lane-when i{background:var(--coral)}.lane-then i{background:var(--green)}.steps{display:grid;gap:7px}.step>span{display:block}.step details{margin-top:6px}.step summary{color:var(--muted);font-size:10px;cursor:pointer}.step pre{max-height:240px;margin:7px 0 0;padding:11px;overflow:auto;border-radius:8px;background:#17202a;color:#dce8e5;font:11px/1.5 ui-monospace,SFMono-Regular,Menlo,monospace;white-space:pre-wrap}.source{margin-top:auto;padding:11px 22px;border-top:1px solid var(--line);color:#8a8e89;font:10px ui-monospace,SFMono-Regular,Menlo,monospace;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.source a{text-decoration:none}.source a:hover{color:var(--coral)}.diagnostics{margin-top:45px;padding:16px 20px;border:1px solid #dda93a77;border-radius:12px;background:#dda93a0c}.diagnostics summary{cursor:pointer;font-weight:700}.diagnostics li{margin:8px 0;color:var(--muted)}.empty{display:none;padding:80px 20px;text-align:center;color:var(--muted)}.footer{padding:25px;text-align:center;color:var(--muted);font-size:12px;border-top:1px solid var(--line)}
@media(max-width:900px){.shell{display:block}.sidebar{position:relative;height:auto;padding:18px 24px}.sidebar nav,.nav-label,.side-note{display:none}.brand{margin:0}.hero{padding-top:50px}.scenario-grid{grid-template-columns:1fr}.metrics{grid-template-columns:repeat(2,1fr)}.metric:nth-child(2){border-right:0}.metric:nth-child(-n+2){border-bottom:1px solid #ffffff1c}.filter{overflow:auto}.filter button{white-space:nowrap}.print{display:none}.legend{align-items:start;flex-direction:column}}
@media(max-width:600px){.toolbar{padding:10px;flex-wrap:wrap}.search{flex-basis:100%}.filter{width:100%}.hero{padding:42px 22px}.hero h1{font-size:44px}.main{padding:35px 14px 70px}.legend{margin-bottom:36px}.scenario-head{display:block}.tags{margin-top:10px}.lane{grid-template-columns:1fr}.lane-label{padding:0}.surface-totals{flex-wrap:wrap}}
@media print{.sidebar,.toolbar,.footer{display:none}.shell{display:block}.hero{padding:28px;background:white;color:var(--ink);border-bottom:2px solid var(--ink)}.hero>p,.kicker{color:var(--muted)}.metrics{border-color:var(--line);background:white}.metric{border-color:var(--line)}.main{padding:30px 0}.scenario-grid{display:block}.scenario{break-inside:avoid;margin-bottom:14px;box-shadow:none}.scenario:hover{transform:none}.step details{display:none}}
</style>
</head>
<body>
<div class="shell">
<aside class="sidebar"><div class="brand"><span class="mark">S</span><span>Synopsis</span></div><div class="nav-label">The story by module</div><nav>{{NAVIGATION}}</nav><div class="side-note">Generated from executable specifications. The prose is navigation; the source is the evidence.</div></aside>
<div class="page">
<header class="hero"><span class="kicker">Living behavior · Given / When / Then</span><h1>{{TITLE}}</h1><p>{{DESCRIPTION}}</p><div class="metrics"><div class="metric"><b>{{MODULES}}</b><span>Modules</span></div><div class="metric"><b>{{FEATURES}}</b><span>Features</span></div><div class="metric"><b>{{SCENARIOS}}</b><span>Scenarios</span></div><div class="metric"><b>{{OUTCOMES}}</b><span>Outcomes</span></div></div></header>
<div class="toolbar"><label class="search"><span>⌕</span><input id="search" type="search" placeholder="Find a behavior, outcome, module…" aria-label="Search behaviors"></label><div class="filter" role="group" aria-label="Filter by surface"><button class="active" data-filter="all">All</button><button data-filter="backend">Backend</button><button data-filter="frontend">Frontend</button><button data-filter="model">Model</button></div><button class="print" onclick="print()">Print</button></div>
<main class="main"><div class="legend"><p><strong>This is the system through examples.</strong><br>Every card connects context, action, and observable result back to executable source.</p><div class="surface-totals"><span><i class="back"></i>{{BACKEND}} backend</span><span><i class="front"></i>{{FRONTEND}} frontend</span><span><i class="model"></i>{{MODEL}} model</span></div></div><div id="content">{{CONTENT}}</div><div id="empty" class="empty"><h2>No matching behaviors</h2><p>Try a broader phrase or another surface.</p></div>{{DIAGNOSTICS}}</main>
<footer class="footer">Made from behavior, not comments · Cratis Synopsis</footer>
</div></div>
<script>
const search=document.querySelector('#search'),cards=[...document.querySelectorAll('.scenario')],empty=document.querySelector('#empty');let surface='all';
function filter(){const q=search.value.trim().toLowerCase();let visible=0;cards.forEach(card=>{const show=(surface==='all'||card.dataset.surface===surface)&&(!q||card.dataset.search.includes(q));card.hidden=!show;if(show)visible++});document.querySelectorAll('.feature').forEach(x=>x.hidden=![...x.querySelectorAll('.scenario')].some(c=>!c.hidden));document.querySelectorAll('.module').forEach(x=>x.hidden=![...x.querySelectorAll('.scenario')].some(c=>!c.hidden));empty.style.display=visible?'none':'block'}
search.addEventListener('input',filter);document.querySelectorAll('[data-filter]').forEach(button=>button.addEventListener('click',()=>{surface=button.dataset.filter;document.querySelectorAll('[data-filter]').forEach(x=>x.classList.toggle('active',x===button));filter()}));
</script>
</body>
</html>
""";
}
