---
applyTo: "**/*.cs"
paths:
  - "**/*.cs"
---

# C# conventions

Use modern C# with file-scoped namespaces, nullable reference types, `var` when the type is apparent, immutable
records for data, and primary constructors where they improve clarity. Public APIs need multiline XML
documentation. Prefer small focused types and language-neutral names in the shared model. Never catch and
silently discard an exception. Apply `.editorconfig` and keep Release builds warning-free.
