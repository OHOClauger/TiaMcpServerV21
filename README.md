# TIA-Portal MCP-Server

An MCP server that connects LLMs to Siemens TIA Portal via the Openness API. Browse projects, export/import blocks, manage devices, download to PLCs, and more — all from your AI assistant.

## Quick Start

### 1. Prerequisites

- Windows with **.NET Framework 4.8**
- **Siemens TIA Portal V21** installed and running
- Environment variable `TiaPortalLocation` set to `C:\Program Files\Siemens\Automation\Portal V21`
- User in Windows group **Siemens TIA Openness**

### 2. Download

Download `TiaMcpServer-<version>.zip` from [GitHub Releases](../../releases) and extract it.

### 3. Configure your MCP client

**Claude Code** — add to `.mcp.json` in your workspace:
```json
{
  "servers": {
    "tia-portal": {
      "command": "C:\\path\\to\\TiaMcpServer.exe",
      "args": [],
      "env": {}
    }
  }
}
```

**Claude Desktop** — add to `%APPDATA%\Claude\claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "tia-portal": {
      "command": "C:\\path\\to\\TiaMcpServer.exe",
      "args": [],
      "env": {}
    }
  }
}
```

**VS Code Copilot Chat** — add to `.vscode/mcp.json`:
```json
{
  "servers": {
    "tia-portal": {
      "command": "C:\\path\\to\\TiaMcpServer.exe",
      "args": [],
      "env": {}
    }
  }
}
```

> For TIA Portal V18, V19, or V20, add `"--tia-major-version", "18"` (adjust the number) to `args`.

### 4. Use it

Open TIA Portal, then ask your AI assistant to connect and interact with your project.

## Build from source

```bash
dotnet build -c Release
# Output: src/TiaMcpServer/bin/Release/net48/TiaMcpServer.exe
```

## Available Tools (65)

### Connection & State

| Tool | Description |
|------|-------------|
| `Connect` | Connect to TIA Portal |
| `Disconnect` | Disconnect from TIA Portal |
| `GetState` | Get server state (connection + project) |

### Project Management

| Tool | Description |
|------|-------------|
| `GetProject` | Get open project/session info |
| `OpenProject` | Open a project/session |
| `CreateProject` | Create a new project |
| `SaveProject` | Save the current project |
| `SaveAsProject` | Save project with a new name |
| `ArchiveProject` | Archive the current project as a compressed .zap file |
| `RetrieveProject` | Retrieve (restore) a project from a .zap archive |
| `CloseProject` | Close the current project |
| `GetProjectTree` | Get project structure as tree view |

### Devices

| Tool | Description |
|------|-------------|
| `GetDevices` | List all devices |
| `GetDeviceInfo` | Get device details |
| `GetDeviceItemInfo` | Get device item details |
| `AddDevice` | Add a device from hardware catalog |
| `RemoveDevice` | Remove a device |
| `SearchHardwareCatalog` | Search hardware catalog |

### PLC Software

| Tool | Description |
|------|-------------|
| `GetSoftwareInfo` | Get PLC software info |
| `GetSoftwareTree` | Get software structure/tree |
| `CompileSoftware` | Compile PLC software |
| `CompileHardware` | Compile hardware configuration |

### Blocks (OB, FB, FC, DB)

| Tool | Description |
|------|-------------|
| `GetBlocks` | List blocks |
| `GetBlockInfo` | Get block details |
| `GetBlocksWithHierarchy` | List blocks with group hierarchy |
| `ExportBlock` | Export a single block |
| `ExportBlocks` | Export multiple blocks |
| `ImportBlock` | Import a block |
| `ExportBlocksAsDocuments` | Export blocks as .s7dcl/.s7res (V20+) |
| `ImportBlocksFromDocuments` | Import blocks from .s7dcl/.s7res (V20+) |
| `ExportAsDocuments` | Export single block as document (V20+) |
| `ImportFromDocuments` | Import single block from document (V20+) |

### Types (UDT)

| Tool | Description |
|------|-------------|
| `GetTypes` | List types |
| `GetTypeInfo` | Get type details |
| `ExportType` | Export a type |
| `ExportTypes` | Export multiple types |
| `ImportType` | Import a type |

### PLC Tags

| Tool | Description |
|------|-------------|
| `GetPlcTagTables` | List PLC tag tables |
| `GetPlcTags` | List tags from a tag table |
| `ExportPlcTagTable` | Export a tag table |
| `ImportPlcTagTable` | Import a tag table |

### HMI Screens & Tags (incl. WinCC Unified)

| Tool | Description |
|------|-------------|
| `GetHmiScreens` | List HMI screens |
| `GetHmiScreenTree` | Get full screen inventory as a folder tree (recurses, unlike `GetHmiScreens`) |
| `GetHmiScreenItems` | Get a screen's items/objects tree, incl. attributes and tag/PLC dynamizations |
| `ExportHmiScreen` | Export an HMI screen |
| `ImportHmiScreen` | Import an HMI screen |
| `CloneHmiScreen` | Proof-of-concept: recreate a WinCC Unified screen via the object model (no native Copy in Openness) |
| `RepointScreenBindings` | Replace a tag/faceplate binding value across a screen's dynamizations |
| `GetHmiTags` | List HMI tags (WinCC Unified) with connection, PLC tag/address and data type |
| `ExportHmiTags` | Export an HMI tag table's tags to XML (WinCC Unified) |
| `ImportHmiTags` | Import HMI tags into a tag table (WinCC Unified, override) |

### Development

| Tool | Description |
|------|-------------|
| `EvalCSharp` | Dev tool: compile & run a C# snippet in-process against the live Openness session, without rebuilding/restarting the server |

### Libraries

| Tool | Description |
|------|-------------|
| `GetLibraries` | List available libraries |
| `GetLibraryMasterCopies` | List master copies |
| `CopyFromLibrary` | Copy master copy into PLC |

### Networking

| Tool | Description |
|------|-------------|
| `GetNetworkInterfaces` | Get device network interfaces |
| `GetSubnets` | List project subnets |
| `CreateSubnet` | Create a subnet |
| `ConnectToSubnet` | Connect device to subnet |
| `SetNetworkAttribute` | Set network attribute (e.g., IP) |

### Online & Download

| Tool | Description |
|------|-------------|
| `DownloadToDevice` | Download software to PLC |
| `GoOnline` | Go online with device |
| `GoOffline` | Go offline |

### Safety (F-CPU)

| Tool | Description |
|------|-------------|
| `GetSafetyInfo` | Get safety information |
| `CompileSafety` | Compile safety program |

## TIA Portal Versions

- **V21** is the default version.
- Previous versions (V18, V19, V20) are supported via the `--tia-major-version` argument.
- Export/import as documents (.s7dcl/.s7res) requires TIA Portal V20 or newer.

## Known Limitations

- As of 2025-09-02: Importing Ladder (LAD) blocks from SIMATIC SD documents requires the companion `.s7res` file to contain en-US tags for all items; otherwise import may fail. This is a known limitation/bug in TIA Portal Openness.
 - `ExportBlock` requires a fully qualified `blockPath` like `Group/Subgroup/Name`. If only a name is provided, the MCP server returns `InvalidParams` and may include suggestions for likely full paths.

## Testing

- See `tests/TiaMcpServer.Test/README.md` for environment prerequisites and test asset setup.
- Standard command: `dotnet test` (run from the repo root).
- Test execution policy: offer to run tests, but only execute after explicit user confirmation. Details in `AGENTS.md`.

## Contributing

- See `agents.md` for guidance on working with agentic assistants and the test execution policy (offer to run tests only with explicit user confirmation).

## Error Handling (ExportBlock)

- The Portal layer throws `PortalException` with a short message and `PortalErrorCode` (e.g., NotFound, ExportFailed), and attaches `softwarePath`, `blockPath`, `exportPath` in `Exception.Data` while preserving `InnerException` on export failures.
- The MCP layer maps these to `McpException` codes. For `ExportFailed`, it includes a concise reason from the underlying error; for `NotFound`, it returns `InvalidParams` and may suggest likely full block paths if a bare name was provided.
- Consistency required: TIA Portal never exports inconsistent blocks/types. Single export returns `InvalidParams` with a message to compile first. Bulk export skips inconsistent items and returns them in an `Inconsistent` list alongside `Items`.
- Standardization: Exception context metadata is attached in a single catch per portal method right before rethrow, not at inline throw sites. See `docs/error-model.md`.
- This standardized pattern currently applies to `ExportBlock` and will expand incrementally.

## Transports

- Supported today: `stdio`
  - Program wires `AddMcpServer().WithStdioServerTransport()`.
  - For stdio, logs must go to stderr to avoid corrupting JSON-RPC.
- Available via SDK: `stream` (custom streams)
  - The SDK exposes `WithStreamServerTransport(Stream input, Stream output)` which can be used to host over TCP sockets or other streams.
  - Not wired in this repo yet.
- HTTP/Streamable HTTP: not implemented yet
  - The current ModelContextProtocol .NET package in use (0.3.0-preview.4) does not provide an HTTP server transport out of the box.
  - Plan (see TODO): add `--transport http`, `--http-prefix`, and `--http-api-key`, host with `HttpListener`, and route POST `/mcp` to the MCP handlers. Later align with MCP Streamable HTTP spec.

## VS Code Extension

The original version of this MCP server is also available as a VS Code extension: [TIA-Portal MCP-Server](https://marketplace.visualstudio.com/items?itemName=JHeilingbrunner.vscode-tiaportal-mcp). Note: the extension bundles v0.0.18 and does not include the additional tools from this fork.
