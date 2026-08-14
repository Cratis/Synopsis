# Synopsis — project instructions

Synopsis is a Cratis framework-profile repository. It discovers BDD specifications without executing or
compiling the repository under analysis and turns them into a stable behavior model. The model is the product;
the HTML renderer and CLI are consumers.

## Non-negotiable product behavior

- Never execute analyzed source. Repository input is untrusted data.
- Preserve the Given / When / Then meaning. Do not reduce the output to a test inventory.
- C#, TypeScript, and Screenplay are equal input languages. A language-specific detail must not leak into the
  shared model.
- Keep the generated HTML self-contained, accessible, printable, responsive, and useful without a server.
- The JSON format is a compatibility surface for future Cratis CLI and Studio integrations.
- Discovery must be deterministic: sort paths and model elements ordinally and do not write timestamps into
  comparison-sensitive output unless explicitly requested.
- Be tolerant while reading a repository. Report skipped or ambiguous input as diagnostics; one malformed file
  must not prevent useful output from the rest.

## Engineering conventions

- Use modern C# and file-scoped namespaces. Prefer immutable records for the public model.
- Public APIs require XML documentation. Keep parsers focused by language behind `ISpecificationParser`.
- Add Cratis-style specifications (`Establish`, `Because`, `should_`) for behavior changes.
- Run `dotnet test` and a Release build. Dogfood the CLI against `Samples/Bookshop` after renderer changes.
- Use American English in code and documentation.

## Repository map

- `Source/Synopsis` — language-neutral model, discovery, parsers, and renderers.
- `Source/Tool` — `synopsis` .NET tool and its zero-configuration command line.
- `Source/Specs` — executable behavior specifications.
- `Samples/Bookshop` — representative C#, TypeScript, and Screenplay input.
- `Documentation` — decisions, model contract, and integrations.
