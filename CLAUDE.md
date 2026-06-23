# Claude Code - TiaMcpServer Project Guidelines

## Project

TiaMcpServer is an MCP server (.NET Framework 4.8, Windows only) exposing the
Siemens TIA Portal Openness API to LLMs. Currently provides 49 MCP tools.

License: MIT (J.Heilingbrunner). Fork maintained by JohannPx.

## Architecture

```
src/TiaMcpServer/
├── Program.cs                              <- entry point, DI, auto-discovery [McpServerTool]
├── Siemens/
│   ├── Portal.cs                           <- Openness API wrapper (all TIA Portal calls)
│   ├── PortalException.cs                  <- custom exceptions
│   └── PortalErrorCode.cs                  <- error enum (NotFound, ExportFailed, InvalidParams, InvalidState)
└── ModelContextProtocol/
    ├── McpServer.cs                        <- MCP tools [McpServerTool] (schema + error mapping)
    ├── Responses.cs                        <- response DTOs
    ├── Helper.cs                           <- utilities
    └── Types.cs                            <- shared types
```

## Pattern for adding a new MCP tool

1. Add a method in `Portal.cs` (Openness API call + PortalException on error)
2. Add a `[McpServerTool]` method in `McpServer.cs` (parameter schema + McpException mapping)
3. Add a Response class in `Responses.cs`
4. Auto-discovered by the SDK - nothing else to modify

Each tool is typically ~70-110 lines across these 3 files.

## C# Style

- Target: .NET Framework 4.8 - no `GetValueOrDefault`, no C# 12+ features, no `IEnumerable` iteration on non-iterable types
- 4 spaces indentation, opening braces on new line
- PascalCase for classes and public members, camelCase for parameters and locals
- Usings grouped at top, separated from namespace by one blank line
- Prefer `Task`/`Task<T>` for long-running operations
- Use `Microsoft.Extensions.Logging` for logging
- See `style.md` for full details

## Error Handling

- Portal layer: throw `PortalException` with `PortalErrorCode`
  - Available codes: `NotFound`, `ExportFailed`, `InvalidParams`, `InvalidState`
- MCP layer: map to `McpException` with `McpErrorCode`
- Attach context (`softwarePath`, `blockPath`, `exportPath`) in `Exception.Data`, not in the message
- Single decoration point: one catch block per portal method, just before rethrow
- See `docs/error-model.md` for full specification

## Build and Test

```powershell
# Build
dotnet build -c Release
# Output: src/TiaMcpServer/bin/Release/net48/TiaMcpServer.exe

# Test (requires TIA Portal V20 installed - ask user before running)
dotnet test

# Run directly
dotnet run --project src/TiaMcpServer/TiaMcpServer.csproj
```

## Test Execution Policy

- ALWAYS ask for explicit user confirmation before running tests
- Tests require: TIA Portal V20 installed, .NET Framework 4.8, env var `TiaPortalLocation`, Windows group "Siemens TIA Openness"
- If tests fail due to environment, report the error and suggest alternatives
- MSTest with `[TestClass]` and `[TestMethod]`, test files named `Test<Area>.cs`

## Encoding

- UTF-8 with BOM, CRLF line endings (Windows)
- Do not modify existing file encodings
- Markdown: fenced code blocks with language hints

## Release Process

1. Bump `AssemblyVersion` in `src/TiaMcpServer/TiaMcpServer.csproj`
2. Update `CHANGELOG.md` with new entry
3. Commit and push to `main`
4. Tag: `git tag vX.Y.Z && git push origin vX.Y.Z`
5. GitHub Actions automatically builds and creates a release with the zip artifact

## Available Siemens Openness DLLs

Referenced via NuGet (Siemens.Collaboration.Net.TiaPortal.Openness.Resolver):
- `Siemens.Engineering` (core: HW, SW, Compiler, Library, Safety, Download, Online)
- `Siemens.Engineering.Hmi` (HMI Classic/Comfort)

Already imported namespaces in Portal.cs:
- Siemens.Engineering.HW, HW.Features
- Siemens.Engineering.Hmi, HmiUnified
- Siemens.Engineering.Library, Library.MasterCopies
- Siemens.Engineering.Safety
- Siemens.Engineering.Download, Download.Configurations
- Siemens.Engineering.Online
- Siemens.Engineering.SW, SW.Blocks, SW.Tags, SW.Types
- Siemens.Engineering.Compiler, Multiuser

## Key Constraints

- Windows only (.NET Framework 4.8, not .NET Core)
- TIA Portal V20 is the default; older versions via `--tia-major-version` flag
- The `System.Management.dll` reference uses a hardcoded HintPath
- Export as documents (.s7dcl/.s7res) requires V20+
- LAD import requires en-US tags in .s7res files (known TIA Openness limitation)
