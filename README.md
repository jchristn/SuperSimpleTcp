![alt tag](https://github.com/jchristn/simpletcp/blob/master/assets/icon.ico)

# SimpleTcp

## Simple wrapper for TCP client and server in C# with SSL support

[![nuget](https://badge.fury.io/nu/Object.svg)](https://www.nuget.org/packages/SuperSimpleTcp/)     

SimpleTcp provides simple methods for creating your own TCP-based sockets application, enabling easy integration of connection management, sending, and receiving data.  SimpleTcp does NOT provide message framing.  If you need framing (or don't know what framing is), please see WatsonTcp. 
 
## New in v2.0.2

- Support for specifying the server hostname as an alternative to its IP when instantiating the client (thanks @OpNop!)

## Help or Feedback

Need help or have feedback?  Please file an issue here!

## Simple Examples

### Server Example
```
using SimpleTcp;

void Main(string[] args)
{
	// instantiate
	TcpServer server = new TcpServer("127.0.0.1", 9000, false, null, null);

	// set callbacks
	server.ClientConnected += ClientConnected;
	server.ClientDisconnected += ClientDisconnected;
	server.DataReceived += DataReceived;

	// let's go!
	server.Start();

	// once a client has connected...
	server.Send("[ClientIp:Port]", Encoding.UTF8.GetString("Hello, world!"));
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
```
using SimpleTcp;

void Main(string[] args)
{
	// instantiate
	TcpClient client = new TcpClient("127.0.0.1", 9000, false, null, null);

	// set callbacks
	client.Connected += Connected;
	client.Disconnected += Disconnected;
	client.DataReceived += DataReceived;

	// let's go!
	client.Connect();

	// once connected to the server...
	client.Send(Encoding.UTF8.GetBytes("Hello, world!"));
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

Both TcpClient and TcpServer have settable values for:

- ```Logger``` - method to invoke to send log messages from either TcpClient or TcpServer
- ```MutuallyAuthenticate``` - only used if SSL is enabled, demands that both client and server mutually authenticate
- ```AcceptInvalidCertificates``` - accept and allow certificates that are invalid or cannot be validated

TcpServer also has:

- ```IdleClientTimeoutSeconds``` - automatically disconnect a client if data is not received within the specified number of seconds

Additionally, both TcpClient and TcpServer offer a statistics object under ```TcpClient.Stats``` and ```TcpServer.Stats```.  These values (other than start time and uptime) can be reset using the ```Stats.Reset()``` API.

### Testing with SSL

A certificate named ```simpletcp.pfx``` is provided for simple testing.  It should not expire for a really long time.  It's a self-signed certificate and you should NOT use it in production.  Its export password is ```simpletcp```.

## Disconnection Handling

The project TcpTest (https://github.com/jchristn/TcpTest) was built specifically to provide a reference for SimpleTcp to handle a variety of disconnection scenarios.  These include:

| Test case | Description | Pass/Fail |
|---|---|---|
| Server-side dispose | Graceful termination of all client connections | PASS |
| Server-side client removal | Graceful termination of a single client | PASS |
| Server-side termination | Abrupt termination due to process abort or CTRL-C | PASS |
| Client-side dispose | Graceful termination of a client connection | PASS |
| Client-side termination | Abrupt termination due to a process abort or CTRL-C | PASS |

## Running under Mono

.NET Core is the preferred environment for cross-platform deployment on Windows, Linux, and Mac.  For those that use Mono, SimpleTcp should work well in Mono environments.  It is recommended that you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).

```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```

## Version History

Please refer to CHANGELOG.md.
