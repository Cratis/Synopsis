# Integrating Synopsis

Choose how the team will use the result: download a CI artifact, host the HTML on GitHub Pages, or generate it
locally on demand. These are alternatives, not a required sequence.

The CI excerpt and local target below assume a .NET SDK and a local tool manifest. For a repository without
a manifest, set up the tool once:

```bash
dotnet new tool-manifest
dotnet tool install Cratis.Synopsis.Tool
```

If a manifest already exists, use it rather than creating another; install `Cratis.Synopsis.Tool` only if it
is not already declared. Keep the manifest in the repository so CI can restore the declared tool. For
standalone use, see the [Quick start](../README.md#quick-start).

## CI artifact

Run Synopsis after the specs. This YAML is a steps excerpt, not a complete workflow: checkout and .NET SDK
setup must come first, and the local tool must be declared in the manifest. The test step represents your
repository's own test gate; adapt it to that gate rather than adding a second test run.

```yaml
- run: dotnet test --configuration Release
- run: dotnet tool restore
- run: dotnet tool run synopsis . --format both --output Artifacts/synopsis.html --fail-on-empty
- uses: actions/upload-artifact@v7
  with:
    name: system-synopsis
    path: Artifacts/synopsis.*
```

`--fail-on-empty` returns exit code 2 when no supported specifications are found, which can flag a misplaced
source root. Synopsis reads specifications without executing them; the preceding test gate is responsible
for checking their results.

With `--format both`, the generation step writes:

- `Artifacts/synopsis.html`: the portable HTML document for readers.
- `Artifacts/synopsis.json`: the generated [behavior model](behavior-model.md) for tools, not the optional
  input [configuration file](configuration.md).

After the workflow succeeds, download `system-synopsis` and open its `synopsis.html`. Check that a known
behavior from the analyzed repository appears in the document. The tool's console summary reports the
scenarios, outcomes, modules, and output paths; finding some specifications does not establish complete
coverage.

## GitHub Pages

For hosted access rather than an artifact download, adapt your repository's Pages deployment workflow.
Generate `site/index.html`, upload it with `actions/upload-pages-artifact`, and deploy with
`actions/deploy-pages`. The output has no runtime or asset-path assumptions.

## Local development

For on-demand local generation, add a convenience target only if the team wants it. On a fresh checkout,
run `dotnet tool restore` before using the target:

```xml
<Target Name="Synopsis" AfterTargets="Test" Condition="'$(GenerateSynopsis)' == 'true'">
  <Exec Command="dotnet tool run synopsis &quot;$(MSBuildProjectDirectory)&quot; --output &quot;$(MSBuildProjectDirectory)/Artifacts/synopsis.html&quot;" />
</Target>
```

This target requests HTML only. It is opt-in because documentation generation should not tax or mutate every
normal build.

## Cratis product path

This roadmap describes future product integration, not setup required to generate documentation. The
[product decisions](decisions.md) record the rationale:

1. Ship and harden the standalone tool and `Cratis.Synopsis` library.
2. Let `cratis synopsis` provide discoverability by hosting the library.
3. Replace the tolerant `.play` reader with a Screenplay syntax-tree adapter.
4. Let Studio visualize `synopsis.json` beside an event model.
5. Overlay Stage specification run results without making execution mandatory.
