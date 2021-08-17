# Change Log

## Current Version

v2.4.1

- Automatic client-side timeout and disconnect due to server inactivity; see ```Settings.IdleServerTimeoutMs```

## Previous Versions

v2.4.0

- Breaking change, timeouts now use milliseconds instead of seconds (does not apply to keepalive settings)

v2.3.0

- ConnectWithRetries method to continually retry connecting to the server over a specified number of seconds

v2.2.1

- Breaking change; TCP keepalives now disabled by default due to incompatibility and problems on some platforms

v2.2.0
 
- Breaking changes
- Consolidated event argument objects to provide clients with context on which server is connected/disconnected or sent data
  - ```DataReceivedFromClientEventArgs``` and ```DataReceivedFromServerEventArgs``` consolidated into ```DataReceivedEventArgs```
  - ```SimpleTcpClient.Connected``` and ```SimpleTcpClient.Disconnected``` now use ```ClientConnectedEventArgs``` and ```ClientDisconnectEventArgs```
  - Motivation for the change was to support applications that use multiple instances of SimpleTcpClient with each instance using the same event handlers

v2.1.0

- Breaking changes
- Retarget to include .NET Core (includes previous targeting to .NET Standard and .NET Framework)
- Consolidated settings and event classes
- Added support for TCP keepalives
- Client ```.Disconnect()``` API
- Additional constructors

v2.0.6

- Async APIs
- SemaphoreSlim fix

v2.0.5

- Fix for ```ClientMetadata.Dispose```

v2.0.4

- Fix for server constructor ```IPAddress.Any``` use case in addition to use of server hostname instead of IP address

v2.0.3

- Minor rework of idle client timeout handling due to intermittent issue (thank you @kopkarmecoindo)

v2.0.2

- Support for specifying the server hostname as an alternative to its IP when instantiating the client (thanks @OpNop!)

v2.0.1

- Breaking changes; moved from Func-based callbacks to events (thanks @cmeeren)
- Added Statistics object

v1.1.8

- Fix for IsConnected property (thank you @OpNop)

v1.1.7

- Bugfix for idle client timeout not being reset upon receiving data (thanks @pha3z!)

v1.1.6

- Added support for Send(string) 

v1.1.5

- Added support for idle client disconnection

v1.1.4

- XML documentation

v1.1.0

- Dispose fixes, better disconnect handling under five key use cases listed below
- Breaking changes!  Task-based callbacks and minor cleanup

v1.0.x

- Retargeted to both .NET Core 2.0 and .NET Framework 4.6.1.
- ```IsConnected``` property on client
- Initial version with SSL support 
