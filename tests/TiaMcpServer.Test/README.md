# TiaMcpServer.Test

MSTest project verifying portal connectivity, project handling, devices, and MCP server behavior.

## Environment Prerequisites

- .NET Framework 4.8 installed
- Siemens TIA Portal V20 installed and running
- User in Windows group "Siemens TIA Openness"
- Env var `TiaPortalLocation` set to `C:\\Program Files\\Siemens\\Automation\\Portal V20`

## Test Assets
- `assets/TestProject1.zap20` – local project used in tests.
- `TestSession1.als20` – create this multi-user session manually for session tests.

See `Settings.cs` for configuration options such as project paths and timeouts.

## Test Execution Policy

- Offer to run tests, but only execute them after explicit user confirmation. See root `AGENTS.md` for details.
