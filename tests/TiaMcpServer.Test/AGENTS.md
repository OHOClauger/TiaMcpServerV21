# TiaMcpServer.Test Guidelines

- Follow repository style rules in [`../../style.md`](../../style.md).
- Use MSTest attributes `[TestClass]` and `[TestMethod]`.
- Name test files and methods descriptively (e.g., `Test1Portal`).
- Offer to run tests, but only execute them after explicit user confirmation. See the root [`AGENTS.md`](../../AGENTS.md) for the full Test Execution Policy.
- Run `dotnet test` from the repository root after modifying tests.
- Store test assets under the `assets/` directory.
