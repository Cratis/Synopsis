# Contributing

Thank you for helping Synopsis tell system behavior more clearly.

Start with an issue when a change affects the behavior model or adds an input language. Parser changes should
include a small realistic specification, preserve source locations, and degrade to a non-fatal diagnostic for
malformed input. Renderer changes must keep the HTML self-contained, keyboard-usable, responsive, and useful
when printed.

Run the gates before opening a pull request:

```bash
dotnet format Synopsis.slnx --verify-no-changes
dotnet test Synopsis.slnx
dotnet build Synopsis.slnx --configuration Release
dotnet run --project Source/Tool -- Samples/Bookshop --output /tmp/synopsis.html --format both
```

Use the pull request template for release notes. By contributing, you agree that your changes are licensed
under this repository's MIT license.
