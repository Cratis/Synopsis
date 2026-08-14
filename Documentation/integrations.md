# Integrating Synopsis

## CI artifact

Run Synopsis after the specs. `--fail-on-empty` catches a misplaced source root, while `--format both` leaves
HTML for people and JSON for tools:

```yaml
- run: dotnet test --configuration Release
- run: dotnet tool restore
- run: dotnet tool run synopsis . --format both --output Artifacts/synopsis.html --fail-on-empty
- uses: actions/upload-artifact@v7
  with:
    name: system-synopsis
    path: Artifacts/synopsis.*
```

Use a local tool manifest for repeatable builds:

```bash
dotnet new tool-manifest
dotnet tool install Cratis.Synopsis.Tool
```

## GitHub Pages

Generate `site/index.html`, upload it with `actions/upload-pages-artifact`, and deploy with
`actions/deploy-pages`. The output has no runtime or asset-path assumptions.

## Local development

Add a convenience target only if the team wants documentation on demand:

```xml
<Target Name="Synopsis" AfterTargets="Test" Condition="'$(GenerateSynopsis)' == 'true'">
  <Exec Command="dotnet tool run synopsis &quot;$(MSBuildProjectDirectory)&quot; --output &quot;$(MSBuildProjectDirectory)/Artifacts/synopsis.html&quot;" />
</Target>
```

This is opt-in because documentation generation should not tax or mutate every normal build.

## Cratis product path

The recommended evolution is:

1. Ship and harden the standalone tool and `Cratis.Synopsis` library.
2. Let `cratis synopsis` provide discoverability by hosting the library.
3. Replace the tolerant `.play` reader with a Screenplay syntax-tree adapter.
4. Let Studio visualize `synopsis.json` beside an event model.
5. Overlay Stage specification run results without making execution mandatory.
