# Repository Style Guide

## C# Code
- Target .NET Framework 4.8.
- Use four spaces for indentation; no tabs.
- Place opening braces on a new line.
- Use `PascalCase` for classes and public members, `camelCase` for parameters and locals.
- Group `using` directives at the top of files, separated from namespace declarations by one blank line.
- Prefer asynchronous `Task`/`Task<T>` methods when operations may be long running.
- Use `Microsoft.Extensions.Logging` for logging.

## Tests
- Use MSTest with `[TestClass]` and `[TestMethod]` attributes.
- Test file names follow the pattern `Test<Area>.cs`.
- Keep test assets in the `assets/` subfolder.

## Markdown
- Use `#` for headings with a blank line after each heading block.
- Use fenced code blocks with language hints, e.g. ```csharp or ```json.
- Wrap lines at a reasonable length for readability.
