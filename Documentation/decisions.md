# Product decisions

## D-1 — Synopsis is a standalone product

Synopsis owns repository discovery, the language-neutral behavior model, and presentation. It begins as a
global .NET tool and reusable library.

This boundary keeps it useful for any repository, including those that do not use Cratis, and keeps analysis
out of normal compilation. Generating a large document on every inner-loop build would make builds slower,
write surprising files, and couple documentation availability to MSBuild. The recommended build integration
is therefore an explicit CI step after specifications pass.

The Cratis CLI can later expose `cratis synopsis` by referencing the library or forwarding to the tool. That
is a distribution choice, not where the capability should live.

## D-2 — Screenplay, Stage, and Studio integrate through the model

- **Screenplay** is an input. Synopsis reads its first-class `specification` blocks alongside code specs. The
  Screenplay compiler should eventually expose its syntax tree through an adapter rather than duplicating the
  small tolerant reader used in v1.
- **Stage** runs Screenplay specifications. A future run-results overlay can decorate Synopsis scenarios with
  passed, failed, duration, and last-run evidence. Stage should not own documentation generation.
- **Studio** is a natural viewer. It can consume `synopsis.json`, select a module, feature, or slice, and deep
  link to the relevant source. The portable HTML remains useful outside Studio.

The JSON model is versioned from its first release so those integrations do not depend on HTML structure.

## D-3 — Static analysis, never execution

Analyzed repositories are untrusted input. Synopsis reads syntax and names but does not restore, compile,
load, or execute them. C# uses Roslyn syntax trees without semantic compilation; JavaScript/TypeScript uses a
balanced source scanner; Gherkin and Screenplay use small tolerant readers for their declarative syntax.

This also makes the first result fast and useful when the target repository does not currently build.

## D-4 — One portable HTML file is the primary experience

The default result contains its CSS, interaction, and content in one file. It works from disk, in a CI
artifact, as a GitHub Pages asset, in an email attachment, and in an iframe. Search, surface filters, source
links, responsive layout, print styling, and expandable code evidence all work without a server.

JSON is the secondary format for machines and richer hosts. A multi-page renderer can be added behind the
same model when repository size makes it worthwhile.

## D-5 — Folder language is domain language

Cratis specifications deliberately encode meaning in `for_`, `when_`, `and_`, and `given` folders, namespaces,
and types. Synopsis treats that structure as information. Explicit suite, feature, and scenario prose wins;
namespace and path conventions fill missing module, feature, subject, and context. Infrastructure segments
such as `Source`, `Core`, `DotNET`, and spec-project names are stripped. Applications with another root can
configure `skipSegments`; Screenplay and Gherkin declarations always win when present.
