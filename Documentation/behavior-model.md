# Behavior model

`synopsis.json` is the stable integration surface. The root carries `schemaVersion`, title, source identity,
modules, and non-fatal diagnostics. The hierarchy is:

```text
SynopsisDocument
└── module
    └── feature
        └── scenario
            ├── given[]
            ├── when
            ├── then[]
            └── source
```

A scenario also carries its subject, language, and surface (`Backend`, `Frontend`, or `Model`). A step has
readable text and may contain source evidence. Locations are repository-relative and optionally contain a
browser URL.

The model intentionally does not contain C# symbols, TypeScript AST nodes, test-runner objects, or HTML. New
input languages and new renderers meet at this boundary. Consumers must check `schemaVersion`; additive fields
within `1.x` are compatible, while changed meaning requires a new major schema version.
