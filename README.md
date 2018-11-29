# SimpleTcp

## Simple wrapper for TCP client and server in C# with SSL support

For a sample app exercising SimpleTcp, please refer to the ClientTest and ServerTest projects or see below.

SimpleTcp provides simple methods for creating your own TCP-based sockets application.  It is important to note that SimpleTcp does NOT provide message framing.  If you need a solution that provides framing, please see WatsonTcp.  If you don't know what framing is, you probably need WatsonTcp instead of SimpleTcp.

SimpleTcp is available on NuGet.

## Help or Feedback

Need help or have feedback?  Please file an issue here!

## New in v1.0.0

- Initial version with SSL support 

## Simple Examples

### Server Example
```
using SimpleTcp;

void Main(string[] args)
{
	// instantiate
	TcpServer server = new TcpServer("127.0.0.1", 9000, false, null, null);

	// set callbacks
	server.ClientConnected = ClientConnected;
	server.ClientDisconnected = ClientDisconnected;
	server.DataReceived = DataReceived;

	// let's go!
	server.Start();
	Console.ReadKey();
}

bool ClientConnected(string client)
{
	Console.WriteLine(client + " connected");
	return true;
} 

bool ClientDisconnected(string client)
{
	Console.WriteLine(client + " disconnected");
	return true;
}

bool DataReceived(string client, byte[] data)
{
	Console.WriteLine(client + ": " + Encoding.UTF8.GetString(data));
	return true;
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
	server.Connected = Connected;
	server.Disconnected = Disconnected;
	server.DataReceived = DataReceived;

	// let's go!
	client.Connect();
	Console.ReadKey();
}

bool Connected()
{
	Console.WriteLine("Connected");
	return true;
} 

bool Disconnected()
{
	Console.WriteLine("Disconnected");
	return true;
}

bool DataReceived(byte[] data)
{
	Console.WriteLine(Encoding.UTF8.GetString(data));
	return true;
}
```

### Additional Configuration Options

Both TcpClient and TcpServer have settable values for:

- ```ConsoleLogging``` - enable or disable logging to the console
- ```MutuallyAuthenticate``` - only used if SSL is enabled, demands that both client and server mutually authenticate
- ```AcceptInvalidCertificates``` - accept and allow certificates that are invalid or cannot be validated

### Testing with SSL

A certificate named ```simpletcp.pfx``` is provided for simple testing.  It should not expire for a really long time.  It's a self-signed certificate and you should NOT use it in production.  Its export password is ```simpletcp```.

## Running under Mono

SimpleTcp should work well in Mono environments.  It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).

```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```

## Version History

Notes from previous versions (starting with v1.0.0) will be moved here.
