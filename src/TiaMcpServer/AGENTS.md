# src/TiaMcpServer Guidelines

- Adhere to repository-wide style rules in [`../../style.md`](../../style.md).
- Place new MCP tools under `ModelContextProtocol/` and Siemens API wrappers under `Siemens/`.

## Agent Policy

- Follow the root [`AGENTS.md`](../../AGENTS.md) for general guidance.
- Tests and operations that depend on the user environment (e.g., TIA Portal) should only be run after explicit user confirmation.
