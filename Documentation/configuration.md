# Configuration

Configuration is optional. Start with the [Quick start](../README.md#quick-start), then add settings when
repository vocabulary or source linking needs help.

This page describes the input `synopsis.json` that you author, not the generated
[behavior model](behavior-model.md). Put the input file at the analyzed root; Synopsis also searches parent
directories for it. Use `--config` to select a different configuration file.

For example:

```json
{
  "title": "Ada — how it behaves",
  "description": "The promises Ada makes to candidates, customers, and consultants.",
  "sourceUrl": "https://github.com/Hive-Consulting-Community/Ada",
  "skipSegments": ["Application"],
  "exclude": ["Generated", "Snapshots"]
}
```

| Setting | Meaning |
| --- | --- |
| `title` | Cover title; defaults to `<folder> Synopsis`. |
| `description` | One-sentence orientation for the reader. |
| `sourceUrl` | GitHub repository URL. Inferred from `origin` when possible. |
| `skipSegments` | Folder names ignored when inferring modules and features. |
| `exclude` | Additional directory names whose files are excluded from discovery. |

Command-line values take precedence for `title`, `description`, and `sourceUrl`. A nonempty
`--skip-segments` list replaces the configured `skipSegments`; `--exclude` entries are added to the file's
`exclude` list.

Built-in exclusions cover source-control and IDE metadata, package and compiler output, generated web bundles
(`dist`, `out`, `wwwroot`, Storybook output), coverage, artifacts, and test results.

Use `skipSegments` to change grouping without excluding specifications. In the example, `Application` is
ignored when inferring module and feature names, but specifications beneath it can still contribute to the
document. Use `exclude` to omit specifications beneath `Generated` or `Snapshots`, rather than merely hiding
those names from the grouping.

Keep generated output separate from these input settings. The [integration guide](integrations.md) uses
`--format both` with `--output Artifacts/synopsis.html` to produce `Artifacts/synopsis.html` for readers and
`Artifacts/synopsis.json` for tools. That JSON file contains the generated behavior model, not configuration
to edit. The guide also describes how to share the HTML or generate it on demand.
