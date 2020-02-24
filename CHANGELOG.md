# Change Log

## Current Version

v2.0.2

- Support for specifying the server hostname as an alternative to its IP when instantiating the client (thanks @OpNop!)

## Previous Versions

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
