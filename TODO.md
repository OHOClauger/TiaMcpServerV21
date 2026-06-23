# TODO / Enhancements

Centralized list of actionable improvements gathered from initial repo review. Use this to track, prioritize, and reference across PRs. See file paths in backticks.

## Documentation
- [ ] Add a "CLI Options" section to `README.md` documenting `--tia-major-version <int>` and `--logging <1|2|3>` with defaults and effect (1=stderr, 2=Debug, 3=Event Log). Cross-link to samples.
- [x] Add a "Build and Run" section to `README.md` — done as "Installation" section (v0.0.19).
- [x] Add a "Testing" section to `README.md` — already present.
- [ ] Cross-link the `samples/` directory from `README.md`; reference `samples/vscode/mcp.json` and `samples/claude/claude_desktop_config.json`.
- [x] Reduce duplication between `gemini.md` and `src/TiaMcpServer/README.md` — `gemini.md` marked as deprecated, `CLAUDE.md` is now the source of truth.
- [x] Expand Known Limitations — already documented in README.

## CLI / Logging
- [ ] Update `src/TiaMcpServer/CliOptions.cs` `Logging` comment to match current numeric modes (1=stderr, 2=Debug, 3=Event Log) or switch to string values (e.g., "stdio", "debug", "eventlog"). Align parsing and docs accordingly.
- [ ] In `src/TiaMcpServer/Program.cs`, remove hard-coded `options.Logging = 1;` override so CLI-provided logging is honored. Instead, set default only when not provided.
- [ ] Document logging behavior (destinations, filters, minimum levels) in `README.md` or a dedicated `docs/logging.md` and link it.

## Consistency
- [ ] Standardize naming to "TIA Portal" (no hyphen) across all docs and headings; ensure consistent section titles (e.g., clarify "Copilot Chat" vs. "VS Code").
- [ ] Ensure requirements are consistently listed across docs: `.NET Framework 4.8`, `TIA Portal V20`, env var `TiaPortalLocation`, and Windows group membership.

## Changelog
- [x] Fix typo in `CHANGELOG.md`: "Narketplace" → "Marketplace" — done (v0.0.19).

## Tests
- [ ] In `tests/TiaMcpServer.Test/README.md`, double-check instructions for creating `TestSession1.als20` and referencing paths in `Settings.cs`; link this from the main `README.md` Testing section.
- [ ] Consider documenting how to selectively run tests or skip environment-dependent ones (e.g., via MSTest categories) when TIA is unavailable.

## Housekeeping
- [x] Add a "Contributing" link in `README.md` pointing to `agents.md`.

## Transports (HTTP / TCP)

- [ ] Add CLI flags for transport selection
  - `--transport stdio|http` (default: stdio)
  - `--http-prefix http://127.0.0.1:8765/`
  - `--http-api-key <secret>` (optional; header `X-API-Key`)
- [ ] Implement `RunHttpHost` (MVP) using `HttpListener` on .NET Framework 4.8
  - Bind to loopback by default; configurable via `--http-prefix`
  - Endpoint: `POST /mcp` with `application/json`
  - Forward request body to MCP handler and return JSON response
  - Map validation errors to `400`, unexpected to `500`
  - Optional API key guard (header `X-API-Key`)
- [ ] Prefer SDK’s HTTP transport if/when available
  - If the SDK adds `.WithHttpServerTransport(...)`, replace the manual `HttpListener` host with the official transport.
- [ ] Consider TCP transport via streams as a quick alternative
  - Add a TCP listener and pass the network streams to `.WithStreamServerTransport(input, output)`
  - Useful for remote connectivity prior to HTTP
- [ ] Align with MCP Streamable HTTP spec (follow-up)
  - Honor `MCP-Protocol-Version` header
  - Session handling via `Mcp-Session-Id` when applicable
  - Optional SSE support if clients require streaming
- [ ] Documentation
  - Update root and server README with a "Transports" section (stdio today; streams possible; HTTP planned).
  - Add usage examples for `--transport http` when implemented (curl examples; security notes).

## Siemens Wrappers Refactor (Duplication/Exceptions)

- [ ] Centralize exception handling in Siemens wrappers
  Reasoning: `Portal.cs` contains many `try/catch (Exception)` blocks that return `false`/`null` without consistent logging or context. A small helper reduces boilerplate and improves observability.
  Excerpt (today):
  ```csharp
  try
  {
      _project = null;
      _portal?.Dispose();
      return true;
  }
  catch (Exception)
  {
      return false;
  }
  ```
  Example (proposed helper usage):
  ```csharp
  return Operation.Run(_logger, "Disconnecting from TIA Portal", () =>
  {
      _project = null;
      _portal?.Dispose();
  });
  ```

- [ ] Add guard + not-found helpers for Siemens entities
  Reasoning: Repeated null checks (GetDevice/GetType/GetBlock, etc.) and ad-hoc error messages create inconsistencies. A guard establishes consistent messages and reduces lines.
  Excerpt (today):
  ```csharp
  var device = GetDevice(devicePath);
  if (device == null)
  {
      return false; // or throw later in MCP layer
  }
  ```
  Example (proposed):
  ```csharp
  var device = Guard.RequireNotNull(GetDevice(devicePath),
      () => McpErrors.NotFound("Device", devicePath));
  ```

- [ ] Introduce DTO mappers for attributes → response objects
  Reasoning: Mapping attributes and common fields is repeated across blocks/types/devices. Central mappers keep shape changes consistent.
  Excerpt (today):
  ```csharp
  var attrs = Helper.GetAttributeList(block);
  var dto = new ResponseBlockInfo { Name = block.Name, Attributes = attrs, /* ... */ };
  ```
  Example (proposed):
  ```csharp
  var dto = DtoMapper.ToBlockInfo(block);
  ```

- [ ] Roll out PortalException + context enrichment pattern beyond ExportBlock
  Affected: `ImportBlock`, `ExportBlocks`, `ExportType`, `ImportType`, `ExportBlocksAsDocuments`, `ImportFromDocuments`, etc.
  Rules:
  - Short messages + `PortalErrorCode` only (no param echoing in message)
  - Attach context in `Exception.Data` in a single catch per portal method, just before rethrow (see docs/error-model.md)
  - Preserve `InnerException` for operation failures and log once with structured fields

- [ ] Add helpers for path resolution parity
  - `GetTypePath(PlcType)` analogous to `GetBlockPath(PlcBlock)` for building fully-qualified paths.
  - Use these from MCP when building “Did you mean…” suggestions.

- [ ] Create a list mapping helper for collection projections
  Reasoning: Multiple `foreach` loops project Siemens objects into response lists with null filters. A helper simplifies and standardizes this.
  Excerpt (today):
  ```csharp
  var list = new List<ResponseBlockInfo>();
  foreach (var b in blocks)
  {
      if (b != null) list.Add(DtoMapper.ToBlockInfo(b));
  }
  ```
  Example (proposed):
  ```csharp
  var list = ListMapper.Map(blocks, DtoMapper.ToBlockInfo);
  ```

- [ ] Generalize ASCII tree printing (project/software trees)
  Reasoning: Several recursive methods build prefixed tree strings with near-identical logic. A generic tree printer would remove duplication and reduce bugs.
  Excerpt (today):
  ```csharp
  private void GetProjectTreeDevices(StringBuilder sb, DeviceComposition devices, List<bool> ancestorStates) { /*...*/ }
  private void GetProjectTreeGroups(StringBuilder sb, DeviceUserGroupComposition groups, List<bool> ancestorStates) { /*...*/ }
  ```
  Example (proposed):
  ```csharp
  TreePrinter.Write(sb, root,
      children: n => n.Children,
      label:    n => n.DisplayName,
      hasMore:  n => n.HasMore);
  ```

- [ ] Replace boolean returns with lightweight result objects (internals)
  Reasoning: Widespread `return true/false` makes error sources opaque. A `Result` type can carry messages and improves upstream decisions without changing public MCP contracts yet.
  Excerpt (today):
  ```csharp
  if (!Compile()) return false;
  ```
  Example (proposed):
  ```csharp
  var r = Compile();
  if (!r.Success) return r; // r.Message contains context
  ```

- [ ] Consolidate progress reporting for export/import operations
  Reasoning: ExportBlocks/ExportTypes/ExportBlocksAsDocuments share progress calculations and error notifications. A wrapper reduces scattered try/catch and progress-token checks.
  Excerpt (today):
  ```csharp
  // compute totals, send start; for each item send progress; on error send error progress
  ```
  Example (proposed):
  ```csharp
  await ProgressRunner.Run(total, progressToken, onStart, onItem, onComplete, onError);
  ```

- [ ] Address nullable warnings in `Portal.cs` with guards
  Reasoning: Build shows nullability warnings for software tree groups; explicit guards make intent clear and avoid runtime NREs.
  Excerpt (warnings):
  - CS8602: Dereference of a possibly null reference.
  - CS8604: Possible null reference argument for parameter `blockGroup`/`typeGroup`.
  Example (proposed):
  ```csharp
  var group = Guard.RequireNotNull(blockGroup, () => new InvalidOperationException("Block group missing"));
  GetSoftwareTreeBlockGroup(sb, group, ancestorStates, label, isLast);
  ```
- [ ] Verify that all fenced code blocks in Markdown include language hints per `style.md` and wrap lines for readability.

## MCP Tools Docs (Export/Import)
- [ ] Create per-tool docs under `docs/tools/`:
  - `docs/tools/export-blocks.md`
  - `docs/tools/import-blocks.md`
  Each should include: Overview, Preconditions, Parameters (names/types/defaults), Order of operations (numbered), Error model, Examples (request/response for MCP), Troubleshooting, Performance/limits. Include a Mermaid sequence diagram for call flow.
- [ ] Define a shared error mapping in `docs/error-model.md` (validation → `InvalidParams`, not found → `NotFound`, Openness API → `OpennessError` with native code; guidance for partial vs. overall failure).
- [ ] Add a "Tools" section to `README.md` linking to `docs/tools/` and `docs/error-model.md`; reference `samples/` configs.
- [ ] Add XML documentation comments to export/import methods in `ModelContextProtocol/McpServer.cs` and corresponding Siemens wrappers (e.g., `Siemens/Portal.cs`, `Siemens/Openness.cs`). Cover summary, pre/postconditions, ordered steps, params/returns, exceptions, thread-safety/cancellation, and `<seealso>` links to tool docs.
- [ ] Enable XML documentation file generation in `src/TiaMcpServer/TiaMcpServer.csproj` (set `DocumentationFile` for `net48`) so IDE tooltips and doc generation work.
- [ ] Add usage recipes under `docs/recipes/` (e.g., export only FBs matching `FB_Prod.*`, import with overwrite/skip, preservePath false) with minimal and full payloads and expected responses.

- [ ] Document block path rules and suggestions
  - In `docs/tools/export-blocks.md` and server README, state that `blockPath` must be `Group/Subgroup/Name` and that MCP suggests candidates for single-name inputs by regex searching all blocks and formatting paths via `Portal.GetBlockPath`.
- [ ] Cross-link: from tool docs to relevant tests in `tests/TiaMcpServer.Test` and from code via `<seealso>` to markdown docs; from README to samples and tool docs.
- [ ] Optional: Evaluate DocFX (or similar) to generate API docs from XML comments; if adopted, add a short `docs/README.md` and build instructions.
- [ ] Optional CI: add markdown linting and doc build validation to the pipeline (skippable locally if TIA isn’t installed).

### Version Gating (Export as Documents)
- [ ] Document that `ExportAsDocuments` and `ExportBlocksAsDocuments` require TIA Portal V20+; update prompts and README accordingly.

## Import From Documents (V20+)
- [ ] Add tests for `ImportFromDocuments`: single import happy path, version gating (<20), invalid `importPath`, invalid `fileNameWithoutExtension`.
- [ ] Add tests for `ImportBlocksFromDocuments`: regex filtering on `.s7dcl`, progress notifications, partial failures aggregation, empty directory behavior.
- [ ] Validate enum mapping for `importOption` (Override/None; extend if environment exposes more values).
- [ ] Verify placement into `groupPath` (root vs. nested groups) and behavior when group does not exist.
- [ ] Add docs pages under `docs/tools/` for import-from-documents tools; include file discovery rules (.s7dcl/.s7res), name derivation, and option mapping.

## New Tools v0.0.19 (25 tools added)
- [ ] Test all 25 new tools with a real TIA Portal project (PLC tags, HMI, libraries, network, download, safety, hardware)
- [ ] Add unit tests for new tools (GetPlcTagTables, GetPlcTags, GetHmiScreens, GetLibraries, etc.)
- [ ] Improve SearchHardwareCatalog to browse the actual hardware catalog recursively (currently limited to project devices + HwUtilities)
- [ ] Add proper IConfiguration resolution for DownloadToDevice (currently uses DirectoryInfo overload)
- [ ] Document new tool parameters and usage examples in `docs/tools/`

## CI/CD (v0.0.19)
- [x] GitHub Actions workflow for CI (build on push) and Release (build + zip on tag)
- [x] README updated with full tool catalog (49 tools)
- [x] CHANGELOG updated for v0.0.19
- [x] CLAUDE.md created for Claude Code agent guidelines
