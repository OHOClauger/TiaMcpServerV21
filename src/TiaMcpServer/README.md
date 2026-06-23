# TiaMcpServer

This document provides a comprehensive overview of the TiaMcpServer project, a C# application that acts as a Model Context Protocol (MCP) server to expose the Siemens TIA Portal API to Large Language Models (LLMs).

## 1. Project Overview

The TiaMcpServer project is a .NET 4.8 console application that enables communication between an LLM and the Siemens TIA Portal. It achieves this by implementing an MCP server that exposes a set of tools for interacting with the TIA Portal. The project is divided into two main parts:

*   **MCP Server:** This part of the project is responsible for handling communication with the LLM. It uses the `ModelContextProtocol` library to create an MCP server that listens for requests from the LLM and executes the corresponding tools.
*   **TIA Portal Interfacing API:** This part of the project is responsible for interacting with the TIA Portal. It uses the Siemens TIA Portal Openness API to perform tasks such as connecting to the TIA Portal, opening and closing projects, and working with devices, blocks, and types.

## 2. Project Structure

The project is organized into the following directories:

*   **`ModelContextProtocol/`**: This directory contains the implementation of the MCP server.
    *   `McpServer.cs`: This file defines the MCP tools that can be called by the LLM.
    *   `McpPrompts.cs`: This file contains the prompts that are used to guide the LLM.
    *   `Responses.cs`: This file defines the response objects that are returned by the MCP tools.
    *   `Types.cs`: This file defines the data types that are used by the MCP server.
*   **`Siemens/`**: This directory contains the implementation of the TIA Portal interfacing API.
    *   `Portal.cs`: This file provides a high-level API for interacting with the TIA Portal.
    *   `State.cs`: This file defines the `State` class, which represents the state of the TIA Portal.
    *   `Openness.cs`: This file provides a wrapper around the Siemens TIA Portal Openness API.
*   **`Properties/`**: This directory contains the project's properties, such as the assembly information and launch settings.

## 3. Architecture

The TiaMcpServer project follows a client-server architecture. The LLM acts as the client, and the TiaMcpServer application acts as the server. The communication between the client and the server is handled by the MCP protocol.

The MCP server is responsible for receiving requests from the LLM, executing the corresponding tools, and returning the results. The tools are implemented as methods in the `McpServer` class. These methods use the TIA Portal interfacing API to interact with the TIA Portal.

The TIA Portal interfacing API is implemented in the `Siemens` directory. This API provides a set of classes and methods for performing common tasks, such as connecting to the TIA Portal, opening and closing projects, and working with devices, blocks, and types.

## 4. Functionality (49 MCP Tools)

The TiaMcpServer project provides the following functionality:

*   **Connection & State (3):** `Connect`, `Disconnect`, `GetState`
*   **Project Management (7):** `GetProject`, `OpenProject`, `CreateProject`, `SaveProject`, `SaveAsProject`, `CloseProject`, `GetProjectTree`
*   **Devices (6):** `GetDevices`, `GetDeviceInfo`, `GetDeviceItemInfo`, `AddDevice`, `RemoveDevice`, `SearchHardwareCatalog`
*   **PLC Software (4):** `GetSoftwareInfo`, `GetSoftwareTree`, `CompileSoftware`, `CompileHardware`
*   **Blocks (10):** `GetBlocks`, `GetBlockInfo`, `GetBlocksWithHierarchy`, `ExportBlock`, `ExportBlocks`, `ImportBlock`, `ExportBlocksAsDocuments`, `ImportBlocksFromDocuments`, `ExportAsDocuments`, `ImportFromDocuments`
    - `ExportBlock` expects `blockPath` to be a fully qualified path like `Group/Subgroup/Name`.
    - Export/Import as documents (.s7dcl/.s7res) requires TIA Portal V20+.
*   **Types (5):** `GetTypes`, `GetTypeInfo`, `ExportType`, `ExportTypes`, `ImportType`
*   **PLC Tags (4):** `GetPlcTagTables`, `GetPlcTags`, `ExportPlcTagTable`, `ImportPlcTagTable`
*   **HMI Screens (3):** `GetHmiScreens`, `ExportHmiScreen`, `ImportHmiScreen`
*   **Libraries (3):** `GetLibraries`, `GetLibraryMasterCopies`, `CopyFromLibrary`
*   **Networking (5):** `GetNetworkInterfaces`, `GetSubnets`, `CreateSubnet`, `ConnectToSubnet`, `SetNetworkAttribute`
*   **Online & Download (3):** `DownloadToDevice`, `GoOnline`, `GoOffline`
*   **Safety (2):** `GetSafetyInfo`, `CompileSafety`

## 5. Conclusion

The TiaMcpServer project is a powerful tool that allows LLMs to interact with the Siemens TIA Portal. The project is well-structured and easy to understand. The code is well-commented and follows best practices.

## 6. Future Improvements

*   **Session Path Reliability:** The `GetOpenSessions` method has been updated to return the full path of the session project. However, the TIA Portal Openness API's behavior with multiuser sessions can vary. Future testing should confirm the reliability of retrieving the `Path` for all types of local and remote sessions to ensure the information is always accurate.

## Known Issues

- As of 2025-09-02: Importing Ladder (LAD) blocks from SIMATIC SD documents requires the companion `.s7res` file to contain en-US tags for all items; otherwise import may fail. This is a known limitation/bug in TIA Portal Openness.

## Transports

- Current transport: `stdio`
  - The server is hosted with `AddMcpServer().WithStdioServerTransport()`.
  - For stdio, all logs must go to stderr.
- Streams transport: available in SDK (not wired here)
  - The SDK also exposes `WithStreamServerTransport(Stream input, Stream output)` which can be used to host over TCP or other custom streams.
- HTTP (planned)
  - This repo does not yet include an HTTP or SSE transport. The plan is to add a CLI flag `--transport http` and host a loopback `HttpListener` that forwards POST `/mcp` to the MCP request handler, then iterate towards MCP Streamable HTTP compliance.

## Error Handling Standard (ExportBlock)

- Portal layer
  - Throws `PortalException` with a short message and `PortalErrorCode`.
  - Attaches context via `Exception.Data` keys: `softwarePath`, `blockPath`, `exportPath`.
  - Preserves the original exception as `InnerException` for `ExportFailed` and logs full details.
- MCP layer
  - Maps `NotFound` to `McpException` with `InvalidParams`. If `blockPath` is a single name, it suggests likely full paths by scanning blocks.
  - Maps `ExportFailed` to `InternalError` and includes a concise reason from `InnerException.Message`.
  - Consistency: TIA Portal does not export inconsistent blocks/types. Single-item exports return `InvalidParams` advising to compile first. Bulk exports skip inconsistent items and include them in an `Inconsistent` list in the response.
  - Keeps user messages concise; structured details live in logs and context.
  - Current standardization is applied to `ExportBlock` and will be rolled out to other methods incrementally.
  - Exception metadata: Context keys (e.g., `softwarePath`, `blockPath`/`typePath`, `exportPath`) are attached in a single catch per portal method just before rethrow, not at inline throw sites. See `docs/error-model.md`.

## Contributing

- See root `AGENTS.md` for agent guidance and the test execution policy (offer to run tests only with explicit user confirmation).
