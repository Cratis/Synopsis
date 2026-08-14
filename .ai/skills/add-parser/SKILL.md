# Add or extend a specification parser

1. Add a failing Cratis-style specification using the smallest representative source.
2. Preserve the shared `BehaviorScenario` semantics and source locations.
3. Keep malformed-input handling local and return a diagnostic rather than failing the whole discovery.
4. Add the new extension to discovery only when the parser can safely reject false positives.
5. Run `dotnet test`, a Release build, and generate `Samples/Bookshop/synopsis.html` for visual inspection.
