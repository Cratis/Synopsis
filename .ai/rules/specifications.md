# Specification conventions

Specs communicate behavior to both the runner and Synopsis. Use a `for_<subject>` folder, a `when_<action>`
class or `describe`, optional `given` reusable contexts, and one focused `should_<outcome>` fact / `it` for each
observable outcome. Put setup in `Establish` / `beforeEach`, the trigger in `Because`, and assertions only in the
then members. Names should read as natural language after underscores are replaced with spaces.

Parser specs must include realistic source text and assert the language-neutral result, not implementation
details of Roslyn or the TypeScript scanner.
