![alt tag](https://github.com/jchristn/simpletcp/blob/master/assets/icon.ico)

# SimpleTcp

## Simple wrapper for TCP client and server in C# with SSL support

[![NuGet Version](https://img.shields.io/nuget/v/SuperSimpleTcp.svg?style=flat)](https://www.nuget.org/packages/SuperSimpleTcp/) [![NuGet](https://img.shields.io/nuget/dt/SuperSimpleTcp.svg)](https://www.nuget.org/packages/SuperSimpleTcp)    

SimpleTcp provides simple methods for creating your own TCP-based sockets application, enabling easy integration of connection management, sending, and receiving data.  

- If you need integrated framing, please use WatsonTcp (https://github.com/jchristn/WatsonTcp)
- If you need discrete control over the number of bytes read from or written to a socket, please use CavemanTcp (https://github.com/jchristn/CavemanTcp)
 
## New in v2.2.0

- Breaking changes
- Consolidated data received events to allow clients to know which server sent data

## Help or Feedback

Need help or have feedback?  Please file an issue here!

## Simple Examples

### Server Example
```csharp
using SimpleTcp;

void Main(string[] args)
{
  // instantiate
  SimpleTcpServer server = new SimpleTcpServer("127.0.0.1:9000");

  // set events
  server.Events.ClientConnected += ClientConnected;
  server.Events.ClientDisconnected += ClientDisconnected;
  server.Events.DataReceived += DataReceived;

  // let's go!
  server.Start();

  // once a client has connected...
  server.Send("[ClientIp:Port]", "Hello, world!");
  Console.ReadKey();
}

static void ClientConnected(object sender, ClientConnectedEventArgs e)
{
  Console.WriteLine("[" + e.IpPort + "] client connected");
}

static void ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
{
  Console.WriteLine("[" + e.IpPort + "] client disconnected: " + e.Reason.ToString());
}

static void DataReceived(object sender, DataReceivedFromClientEventArgs e)
{
  Console.WriteLine("[" + e.IpPort + "]: " + Encoding.UTF8.GetString(e.Data));
}
```

### Client Example
```csharp
using SimpleTcp;

void Main(string[] args)
{
  // instantiate
  SimpleTcpClient client = new SimpleTcpClient("127.0.0.1:9000");

  // set events
  client.Events.Connected += Connected;
  client.Events.Disconnected += Disconnected;
  client.Events.DataReceived += DataReceived;

  // let's go!
  client.Connect();

  // once connected to the server...
  client.Send("Hello, world!");
  Console.ReadKey();
}

static void Connected(object sender, EventArgs e)
{
  Console.WriteLine("*** Server connected");
}

static void Disconnected(object sender, EventArgs e)
{
  Console.WriteLine("*** Server disconnected"); 
}

static void DataReceived(object sender, DataReceivedFromServerEventArgs e)
{
  Console.WriteLine("[" + _ServerIp + ":" + _ServerPort + "] " + Encoding.UTF8.GetString(e.Data));
}
```

### Additional Configuration Options

Both SimpleTcpClient and SimpleTcpServer have settable values for:

- ```Logger``` - method to invoke to send log messages from either SimpleTcpClient or SimpleTcpServer
- ```Settings.MutuallyAuthenticate``` - only used if SSL is enabled, demands that both client and server mutually authenticate
- ```Settings.AcceptInvalidCertificates``` - accept and allow certificates that are invalid or cannot be validated
- ```Keepalive``` - to enable/disable keepalives and set specific parameters

SimpleTcpServer also has:

- ```Settings.IdleClientTimeoutSeconds``` - automatically disconnect a client if data is not received within the specified number of seconds

Additionally, both SimpleTcpClient and SimpleTcpServer offer a statistics object under ```SimpleTcpClient.Statistics``` and ```SimpleTcpServer.Statistics```.  These values (other than start time and uptime) can be reset using the ```Statistics.Reset()``` API.

### Local vs External Connections

**IMPORTANT**
* If you specify ```127.0.0.1``` as the listener IP address, it will only be able to accept connections from within the local host.  
* To accept connections from other machines:
  * Use a specific interface IP address, or
  * Use ```null```, ```*```, ```+```, or ```0.0.0.0``` for the listener IP address (requires admin privileges to listen on any IP address)
* Make sure you create a permit rule on your firewall to allow inbound connections on that port
* If you use a port number under 1024, admin privileges will be required

### Testing with SSL

A certificate named ```simpletcp.pfx``` is provided for simple testing.  It should not expire for a really long time.  It's a self-signed certificate and you should NOT use it in production.  Its export password is ```simpletcp```.

## Disconnection Handling

The project TcpTest (https://github.com/jchristn/TcpTest) was built specifically to provide a reference for SimpleTcp to handle a variety of disconnection scenarios.  The disconnection tests for which SimpleTcp is evaluated include:

| Test case | Description | Pass/Fail |
|---|---|---|
| Server-side dispose | Graceful termination of all client connections | PASS |
| Server-side client removal | Graceful termination of a single client | PASS |
| Server-side termination | Abrupt termination due to process abort or CTRL-C | PASS |
| Client-side dispose | Graceful termination of a client connection | PASS |
| Client-side termination | Abrupt termination due to a process abort or CTRL-C | PASS |
| Network interface down | Network interface disabled or cable removed | Partial (see below) |

Additionally, as of v2.1.0, support for TCP keepalives has been added to SimpleTcp, primarily to address the issue of a network interface being shut down, the cable unplugged, or the media otherwise becoming unavailable.  It is important to note that keepalives are supported in .NET Core and .NET Framework, but NOT .NET Standard.  As of this release, .NET Standard provides no facilities for TCP keepalives.

TCP keepalives are enabled by default.
```csharp
server.Keepalive.EnableTcpKeepAlives = true;
server.Keepalive.TcpKeepAliveInterval = 5;      // seconds to wait before sending subsequent keepalive
server.Keepalive.TcpKeepAliveTime = 5;          // seconds to wait before sending a keepalive
server.Keepalive.TcpKeepAliveRetryCount = 5;    // number of failed keepalive probes before terminating connection
```

Some important notes about TCP keepalives:

- Keepalives only work in .NET Core and .NET Framework
- Keepalives can be enabled on either client or server, but are implemented and enforced in the underlying operating system, and may not work as expected
- ```Keepalive.TcpKeepAliveRetryCount``` is only applicable to .NET Core; for .NET Framework, this value is forced to 10

## Running under Mono

.NET Core is the preferred environment for cross-platform deployment on Windows, Linux, and Mac.  For those that use Mono, SimpleTcp should work well in Mono environments.  It is recommended that you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).

```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```

## Version History

Please refer to CHANGELOG.md.
