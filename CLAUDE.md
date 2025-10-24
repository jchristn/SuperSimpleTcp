# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SuperSimpleTcp is a C# library providing a simple wrapper for TCP client and server functionality with SSL/TLS support. It is designed for straightforward TCP communication without built-in message framing (users should understand message framing requirements before using this library). The project targets multiple .NET frameworks: netstandard2.1, net461, net462, net48, net6.0, and net8.0.

## Common Commands

### Build
```bash
dotnet build src/SuperSimpleTcp.sln
```

### Run Tests
```bash
dotnet test src/SuperSimpleTcp.UnitTest/SuperSimpleTcp.UnitTest.csproj
```

### Build NuGet Package
The project is configured to generate a NuGet package on build via `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` in the .csproj file.

### Run Example Applications
```bash
# Server test application
dotnet run --project src/ServerTest/ServerTest.csproj

# Client test application
dotnet run --project src/ClientTest/ClientTest.csproj
```

## Architecture

### Core Components

**SimpleTcpServer** (`SimpleTcpServer.cs`)
- Main server class that listens for and manages multiple client connections
- Uses `TcpListener` for accepting connections
- Maintains connected clients in a `ConcurrentDictionary<string, ClientMetadata>` keyed by "IP:Port"
- Creates dedicated background tasks for each client to receive data via `DataReceiver()`
- Runs `AcceptConnections()` task to accept new client connections
- Runs `IdleClientMonitor()` task to disconnect idle clients based on `Settings.IdleClientTimeoutMs`
- Supports both SSL/TLS and non-SSL connections

**SimpleTcpClient** (`SimpleTcpClient.cs`)
- Main client class for connecting to a TCP server
- Uses `TcpClient` for establishing connections
- Implements `ConnectWithRetries()` for connection retry logic
- Runs three background monitoring tasks after connection:
  - `DataReceiver()` - receives incoming data from server
  - `IdleServerMonitor()` - monitors server activity and disconnects on timeout
  - `ConnectedMonitor()` - periodically polls socket to detect connection loss
- Supports both SSL/TLS and non-SSL connections

**ClientMetadata** (`ClientMetadata.cs`)
- Encapsulates client connection state including `TcpClient`, `NetworkStream`, `SslStream`, and cancellation tokens
- Used by server to track individual client connections
- Implements `IDisposable` for proper resource cleanup

### Event System

Both client and server use event-based notification patterns via dedicated event classes:
- `SimpleTcpClientEvents` / `SimpleTcpServerEvents` - Event handlers container
- `ConnectionEventArgs` - Client connection/disconnection events with `DisconnectReason`
- `DataReceivedEventArgs` - Data received events with `ArraySegment<byte>` for allocation-free receive
- `DataSentEventArgs` - Data sent confirmation events

### Settings Classes

Configuration is managed through settings objects:
- `SimpleTcpServerSettings` - Server configuration (buffer size, max connections, idle timeouts, IP filtering, SSL options)
- `SimpleTcpClientSettings` - Client configuration (buffer size, connection timeouts, idle timeouts, SSL options)
- `SimpleTcpKeepaliveSettings` - TCP keepalive configuration (platform-specific implementation)
- `SimpleTcpStatistics` - Connection statistics (bytes sent/received, uptime)

### Key Design Patterns

**Async/Sync Duality**: Most send/receive operations have both synchronous and asynchronous variants (e.g., `Send()` and `SendAsync()`)

**Allocation-Free Receive**: Version 3.0 introduced allocation-free receive using `ArraySegment<byte>` instead of allocating new byte arrays

**Platform-Specific Code**: Keepalive implementation uses preprocessor directives (`#if NETCOREAPP3_1_OR_GREATER`, `#elif NETFRAMEWORK`, etc.) to handle platform differences

**Connection Monitoring**: Multiple mechanisms detect disconnection:
- Server: `IsClientConnected()` using `IPGlobalProperties` to check TCP connection state
- Client: `PollSocket()` using `Socket.Poll()` and `Socket.Receive()` with `SocketFlags.Peek`
- Both: Idle timeout monitoring via background tasks

**Send Synchronization**: Both client and server use `SemaphoreSlim` to ensure thread-safe sending (`_sendLock` in client, `SendLock` in `ClientMetadata` for server)

### SSL/TLS Support

SSL connections use `SslStream` wrapper over `NetworkStream`:
- Server authenticates as server via `SslStream.AuthenticateAsServerAsync()`
- Client authenticates as client via `SslStream.AuthenticateAsClient()`
- Certificate validation can be customized via `Settings.CertificateValidationCallback`
- Test certificate (`simpletcp.pfx`) provided with password "simpletcp"

### Important Implementation Details

**Nagle's Algorithm**: `NoDelay` is set to `true` by default (Nagle's algorithm disabled) for lower latency

**Stream Buffer Size**: Default 65536 bytes, configurable via `Settings.StreamBufferSize`

**IP:Port Format**: Clients are identified by "IP:Port" strings parsed via `Common.ParseIpPort()`

**Cancellation Tokens**: All async operations and background tasks use linked cancellation tokens for graceful shutdown

**Timeout Handling**: Read timeouts in client return `null`/`default` instead of throwing, allowing the receive loop to continue

## Testing Notes

Unit tests are in `SuperSimpleTcp.UnitTest` and use MSTest framework. Tests require the SSL certificate files (`simpletcp.pfx`, `simpletcp.crt`, `simpletcp.key`) to be copied to output directory.

When testing SSL functionality, be aware that the provided certificate is self-signed for testing purposes only.
