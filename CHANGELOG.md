# Change Log

## [0.0.21] - 2026-06-30

### New Tools (14)
- **HMI Screens (WinCC Unified)**: `GetHmiScreenTree`, `GetHmiScreenItems`, `CloneHmiScreen`, `RepointScreenBindings`
- **HMI Tags (WinCC Unified)**: `GetHmiTags`, `ExportHmiTags`, `ImportHmiTags`
- **Development**: `EvalCSharp` (compile & run C# in-process against the live Openness session, no rebuild/restart)

### Fixes
- COM serialization crash and improved device item path resolution
- Corrects Siemens Engineering DLL loading for TIA Portal V21

### Infrastructure
- Build: project now compiles without the .NET Framework 4.8 Developer Pack installed (`Microsoft.NETFramework.ReferenceAssemblies` package)
- Docs: README updated for TIA Portal V21 (was still documenting V20 as default) and full tool catalog (51 -> 65 tools)
- Bump: AssemblyVersion 0.0.20 -> 0.0.21

## [0.0.20] - 2026-06-02

### New Tools (2)
- **Project**: `ArchiveProject` (archive project as compressed .zap), `RetrieveProject` (restore project from .zap archive)

### Infrastructure
- Docs: Updated README tool catalog (49 -> 51 tools)
- Bump: AssemblyVersion 0.0.19 -> 0.0.20

## [0.0.19] - 2026-03-10

### New Tools (25)
- **PLC Tags**: `GetPlcTagTables`, `GetPlcTags`, `ExportPlcTagTable`, `ImportPlcTagTable`
- **HMI Screens**: `GetHmiScreens`, `ExportHmiScreen`, `ImportHmiScreen`
- **Libraries**: `GetLibraries`, `GetLibraryMasterCopies`, `CopyFromLibrary`
- **Networking (read)**: `GetNetworkInterfaces`, `GetSubnets`
- **Networking (write)**: `CreateSubnet`, `ConnectToSubnet`, `SetNetworkAttribute`
- **Device Management**: `AddDevice`, `RemoveDevice`, `SearchHardwareCatalog`
- **Online/Download**: `DownloadToDevice`, `GoOnline`, `GoOffline`
- **Safety**: `GetSafetyInfo`, `CompileSafety`
- **Compilation**: `CompileHardware`
- **Software**: `GetBlocksWithHierarchy`
- **Project**: `CreateProject`

### Infrastructure
- New: GitHub Actions CI/Release workflow (build on push, release on tag)
- Docs: Updated README with full tool catalog (49 tools)
- Bump: AssemblyVersion 0.0.18 -> 0.0.19

## [0.0.16] - 2025-09-02

- New: ImportFromDocuments and ImportBlocksFromDocuments (V20+)
- Guard: Version checks for export/import as documents (V20+)
- UX: Pre-check .s7res for missing en-US tags; warnings surfaced in responses
- Docs: README updates, prompts note V20+ and known LAD en-US limitation
- Refactor: Updated all McpException throws to SDK signature with McpErrorCode
- Chore: Added TODOs for tests/docs

## [0.0.15] - 2025-08-30

- prompts improved
- long running tasks as async tasks

## [0.0.14] - 2025-08-18

- better structure/tree format
- new GetSoftwareTree()
- bugfixes

## [0.0.13] - 2025-08-14

- logging integrated
- prompts added

## [0.0.12] - 2025-08-07

- export path fixed

## [0.0.11] - 2025-08-07

- project structure formatted as markdown code

## [0.0.10] - 2025-08-07

- tool responses improved

## [0.0.9] - 2025-08-04

- export of blocks and types with 'preservePath' option
- new tools
- some infos with attributes

## [0.0.8] - 2025-08-01

- improved jsonrpc responses
- updated dependencies

## [0.0.7] - 2025-07-18

- new GetState()
- return values fixed

## [0.0.6] - 2025-07-16

- refactored code to use new TIA Portal API
- only blocks (OB/FB/FC/DB) and types (UDT) are now retrieved from the PLC software
- use regex to filter blocks and types
- import of blocks and types to PLC software

## [0.0.5] - 2025-07-11

- locating of plc software by softwarePath. This makes it possible to access plc software in groups/subgroups
- new tool: retrieving of project structure as text
- new tool: compile plc software

## [0.0.4] - 2025-06-30

- opens local session or projects, depending on project file extension

## [0.0.3] - 2025-06-23

- Release on Visual Studio Code Marketplace

