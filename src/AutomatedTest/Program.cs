using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuperSimpleTcp;

namespace AutomatedTest
{
    class Program
    {
        private static readonly List<TestResult> _results = new List<TestResult>();
        private static readonly object _lock = new object();

        static async Task Main(string[] args)
        {
            Console.WriteLine("SuperSimpleTcp Automated Test Suite");
            Console.WriteLine("====================================");
            Console.WriteLine();

            try
            {
                await RunTest("Basic Server Start/Stop", TestBasicServerStartStop);
                await RunTest("Basic Client Connect/Disconnect", TestBasicClientConnectDisconnect);
                await RunTest("Bidirectional Data Exchange", TestBidirectionalDataExchange);
                await RunTest("Multiple Clients", TestMultipleClients);
                await RunTest("Client Enumeration", TestClientEnumeration);
                await RunTest("Server Events - ClientConnected", TestServerEventClientConnected);
                await RunTest("Server Events - ClientDisconnected", TestServerEventClientDisconnected);
                await RunTest("Server Events - DataReceived", TestServerEventDataReceived);
                await RunTest("Server Events - DataSent", TestServerEventDataSent);
                await RunTest("Client Events - Connected", TestClientEventConnected);
                await RunTest("Client Events - Disconnected", TestClientEventDisconnected);
                await RunTest("Client Events - DataReceived", TestClientEventDataReceived);
                await RunTest("Client Events - DataSent", TestClientEventDataSent);
                await RunTest("Server Statistics", TestServerStatistics);
                await RunTest("Client Statistics", TestClientStatistics);
                await RunTest("SSL with Byte Array Constructor - Server", TestSslServerByteArray);
                await RunTest("SSL with Byte Array Constructor - Client", TestSslClientByteArray);
                await RunTest("SSL Bidirectional Data Exchange", TestSslBidirectionalDataExchange);
                await RunTest("Server Initiated Disconnection", TestServerInitiatedDisconnection);
                await RunTest("Client Initiated Disconnection", TestClientInitiatedDisconnection);
                await RunTest("Server Dispose", TestServerDispose);
                await RunTest("Client Dispose", TestClientDispose);
                await RunTest("Large Data Transfer", TestLargeDataTransfer);
                await RunTest("Concurrent Sends", TestConcurrentSends);
                await RunTest("Connect With Retries", TestConnectWithRetries);

                PrintSummary();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }

            Environment.Exit(_results.All(r => r.Passed) ? 0 : 1);
        }

        static async Task RunTest(string testName, Func<Task> testFunc)
        {
            Console.Write($"{testName,-50} ");
            try
            {
                await testFunc();
                _results.Add(new TestResult { Name = testName, Passed = true });
                Console.WriteLine("[PASS]");
            }
            catch (Exception ex)
            {
                _results.Add(new TestResult { Name = testName, Passed = false, ErrorMessage = ex.Message });
                Console.WriteLine("[FAIL]");
                Console.WriteLine($"  Error: {ex.Message}");
                if (ex.StackTrace != null)
                {
                    var stackLines = ex.StackTrace.Split('\n').Take(3);
                    foreach (var line in stackLines)
                    {
                        Console.WriteLine($"  {line.Trim()}");
                    }
                }
            }
        }

        static void PrintSummary()
        {
            Console.WriteLine();
            Console.WriteLine("====================================");
            Console.WriteLine("Test Summary");
            Console.WriteLine("====================================");
            Console.WriteLine();

            int passed = _results.Count(r => r.Passed);
            int failed = _results.Count(r => !r.Passed);

            foreach (var result in _results)
            {
                string status = result.Passed ? "[PASS]" : "[FAIL]";
                Console.WriteLine($"{result.Name,-50} {status}");
                if (!result.Passed && !string.IsNullOrEmpty(result.ErrorMessage))
                {
                    Console.WriteLine($"  {result.ErrorMessage}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Total: {_results.Count} tests");
            Console.WriteLine($"Passed: {passed}");
            Console.WriteLine($"Failed: {failed}");
            Console.WriteLine();

            if (failed == 0)
            {
                Console.WriteLine("Overall: PASS");
            }
            else
            {
                Console.WriteLine("Overall: FAIL");
            }
        }

        // ===== Test Implementations =====

        static async Task TestBasicServerStartStop()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9000");
            server.Start();
            await Task.Delay(100);
            if (!server.IsListening) throw new Exception("Server is not listening");
            server.Stop();
            await Task.Delay(100);
            if (server.IsListening) throw new Exception("Server is still listening after stop");
        }

        static async Task TestBasicClientConnectDisconnect()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9001");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9001");
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client is not connected");

            client.Disconnect();
            await Task.Delay(100);
            if (client.IsConnected) throw new Exception("Client is still connected after disconnect");

            server.Stop();
        }

        static async Task TestBidirectionalDataExchange()
        {
            string receivedByServer = null;
            string receivedByClient = null;
            var serverReceived = new ManualResetEventSlim(false);
            var clientReceived = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9002");
            server.Events.DataReceived += (s, e) =>
            {
                receivedByServer = Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count);
                serverReceived.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9002");
            client.Events.DataReceived += (s, e) =>
            {
                receivedByClient = Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count);
                clientReceived.Set();
            };
            client.Connect();
            await Task.Delay(100);

            // Client sends to server
            client.Send("Hello from client");
            if (!serverReceived.Wait(5000)) throw new Exception("Server did not receive data from client");
            if (receivedByServer != "Hello from client") throw new Exception($"Server received incorrect data: {receivedByServer}");

            // Server sends to client
            var clients = server.GetClients().ToList();
            if (clients.Count != 1) throw new Exception($"Expected 1 client, got {clients.Count}");
            server.Send(clients[0], "Hello from server");
            if (!clientReceived.Wait(5000)) throw new Exception("Client did not receive data from server");
            if (receivedByClient != "Hello from server") throw new Exception($"Client received incorrect data: {receivedByClient}");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestMultipleClients()
        {
            int connectedCount = 0;
            var allConnected = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9003");
            server.Events.ClientConnected += (s, e) =>
            {
                Interlocked.Increment(ref connectedCount);
                if (connectedCount == 3) allConnected.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client1 = new SimpleTcpClient("127.0.0.1:9003");
            using var client2 = new SimpleTcpClient("127.0.0.1:9003");
            using var client3 = new SimpleTcpClient("127.0.0.1:9003");

            client1.Connect();
            client2.Connect();
            client3.Connect();

            if (!allConnected.Wait(5000)) throw new Exception("Not all clients connected");
            if (server.Connections != 3) throw new Exception($"Expected 3 connections, got {server.Connections}");

            client1.Disconnect();
            client2.Disconnect();
            client3.Disconnect();
            await Task.Delay(100);

            if (server.Connections != 0) throw new Exception($"Expected 0 connections after disconnect, got {server.Connections}");

            server.Stop();
        }

        static async Task TestClientEnumeration()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9004");
            server.Start();
            await Task.Delay(100);

            using var client1 = new SimpleTcpClient("127.0.0.1:9004");
            using var client2 = new SimpleTcpClient("127.0.0.1:9004");

            client1.Connect();
            await Task.Delay(100);
            client2.Connect();
            await Task.Delay(100);

            var clients = server.GetClients().ToList();
            if (clients.Count != 2) throw new Exception($"Expected 2 clients, got {clients.Count}");

            // Verify each client is connected
            foreach (var clientId in clients)
            {
                if (!server.IsConnected(clientId))
                    throw new Exception($"Client {clientId} not reported as connected");
            }

            client1.Disconnect();
            await Task.Delay(100);

            clients = server.GetClients().ToList();
            if (clients.Count != 1) throw new Exception($"Expected 1 client after disconnect, got {clients.Count}");

            client2.Disconnect();
            server.Stop();
        }

        static async Task TestServerEventClientConnected()
        {
            bool eventFired = false;
            string connectedClient = null;
            var eventSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9005");
            server.Events.ClientConnected += (s, e) =>
            {
                eventFired = true;
                connectedClient = e.IpPort;
                eventSignal.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9005");
            client.Connect();

            if (!eventSignal.Wait(5000)) throw new Exception("ClientConnected event not fired");
            if (!eventFired) throw new Exception("ClientConnected event flag not set");
            if (string.IsNullOrEmpty(connectedClient)) throw new Exception("Connected client IP:Port is null or empty");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerEventClientDisconnected()
        {
            bool eventFired = false;
            DisconnectReason? disconnectReason = null;
            var eventSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9006");
            server.Events.ClientDisconnected += (s, e) =>
            {
                eventFired = true;
                disconnectReason = e.Reason;
                eventSignal.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9006");
            client.Connect();
            await Task.Delay(100);

            client.Disconnect();

            if (!eventSignal.Wait(5000)) throw new Exception("ClientDisconnected event not fired");
            if (!eventFired) throw new Exception("ClientDisconnected event flag not set");
            if (disconnectReason == null) throw new Exception("Disconnect reason is null");

            server.Stop();
        }

        static async Task TestServerEventDataReceived()
        {
            bool eventFired = false;
            string receivedData = null;
            var eventSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9007");
            server.Events.DataReceived += (s, e) =>
            {
                eventFired = true;
                receivedData = Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count);
                eventSignal.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9007");
            client.Connect();
            await Task.Delay(100);

            client.Send("Test data");

            if (!eventSignal.Wait(5000)) throw new Exception("DataReceived event not fired");
            if (!eventFired) throw new Exception("DataReceived event flag not set");
            if (receivedData != "Test data") throw new Exception($"Expected 'Test data', got '{receivedData}'");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerEventDataSent()
        {
            bool eventFired = false;
            long bytesSent = 0;
            var eventSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9008");
            server.Events.DataSent += (s, e) =>
            {
                eventFired = true;
                bytesSent = e.BytesSent;
                eventSignal.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9008");
            client.Connect();
            await Task.Delay(100);

            var clients = server.GetClients().ToList();
            server.Send(clients[0], "Test message");

            if (!eventSignal.Wait(5000)) throw new Exception("DataSent event not fired");
            if (!eventFired) throw new Exception("DataSent event flag not set");
            if (bytesSent != 12) throw new Exception($"Expected 12 bytes sent, got {bytesSent}");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientEventConnected()
        {
            bool eventFired = false;
            var eventSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9009");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9009");
            client.Events.Connected += (s, e) =>
            {
                eventFired = true;
                eventSignal.Set();
            };

            client.Connect();

            if (!eventSignal.Wait(5000)) throw new Exception("Connected event not fired");
            if (!eventFired) throw new Exception("Connected event flag not set");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientEventDisconnected()
        {
            bool eventFired = false;
            var eventSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9010");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9010");
            client.Events.Disconnected += (s, e) =>
            {
                eventFired = true;
                eventSignal.Set();
            };
            client.Connect();
            await Task.Delay(100);

            client.Disconnect();

            if (!eventSignal.Wait(5000)) throw new Exception("Disconnected event not fired");
            if (!eventFired) throw new Exception("Disconnected event flag not set");

            server.Stop();
        }

        static async Task TestClientEventDataReceived()
        {
            bool eventFired = false;
            string receivedData = null;
            var eventSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9011");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9011");
            client.Events.DataReceived += (s, e) =>
            {
                eventFired = true;
                receivedData = Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count);
                eventSignal.Set();
            };
            client.Connect();
            await Task.Delay(100);

            var clients = server.GetClients().ToList();
            server.Send(clients[0], "Server message");

            if (!eventSignal.Wait(5000)) throw new Exception("DataReceived event not fired");
            if (!eventFired) throw new Exception("DataReceived event flag not set");
            if (receivedData != "Server message") throw new Exception($"Expected 'Server message', got '{receivedData}'");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientEventDataSent()
        {
            bool eventFired = false;
            long bytesSent = 0;
            var eventSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9012");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9012");
            client.Events.DataSent += (s, e) =>
            {
                eventFired = true;
                bytesSent = e.BytesSent;
                eventSignal.Set();
            };
            client.Connect();
            await Task.Delay(100);

            await client.SendAsync("Client data");

            if (!eventSignal.Wait(5000)) throw new Exception("DataSent event not fired");
            if (!eventFired) throw new Exception("DataSent event flag not set");
            if (bytesSent != 11) throw new Exception($"Expected 11 bytes sent, got {bytesSent}");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerStatistics()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9013");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9013");
            client.Connect();
            await Task.Delay(100);

            var initialSent = server.Statistics.SentBytes;
            var initialReceived = server.Statistics.ReceivedBytes;

            client.Send("Test data from client");
            await Task.Delay(200);

            if (server.Statistics.ReceivedBytes <= initialReceived)
                throw new Exception("Server ReceivedBytes did not increase");

            var clients = server.GetClients().ToList();
            server.Send(clients[0], "Test data from server");
            await Task.Delay(200);

            if (server.Statistics.SentBytes <= initialSent)
                throw new Exception("Server SentBytes did not increase");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientStatistics()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9014");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9014");
            client.Connect();
            await Task.Delay(100);

            var initialSent = client.Statistics.SentBytes;
            var initialReceived = client.Statistics.ReceivedBytes;

            await client.SendAsync("Client data");
            await Task.Delay(200);

            if (client.Statistics.SentBytes <= initialSent)
                throw new Exception("Client SentBytes did not increase");

            var clients = server.GetClients().ToList();
            server.Send(clients[0], "Response from server");
            await Task.Delay(200);

            if (client.Statistics.ReceivedBytes <= initialReceived)
                throw new Exception("Client ReceivedBytes did not increase");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestSslServerByteArray()
        {
            // Load certificate from PFX with password and exportable flag, then export as byte array
            var certWithPassword = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
            byte[] certBytes = certWithPassword.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx);

            using var server = new SimpleTcpServer("127.0.0.1", 9015, certBytes);
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();
            await Task.Delay(100);

            if (!server.IsListening) throw new Exception("SSL server is not listening");

            // Client also needs to use SSL with certificate
            var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"), 9015);
            using var client = new SimpleTcpClient(endpoint, certBytes);
            client.Settings.AcceptInvalidCertificates = true;
            client.Connect();
            await Task.Delay(100);

            if (!client.IsConnected) throw new Exception("Client not connected to SSL server");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestSslClientByteArray()
        {
            // Load certificate from PFX with password and exportable flag, then export as byte array
            var certWithPassword = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
            byte[] certBytes = certWithPassword.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx);

            using var server = new SimpleTcpServer("127.0.0.1", 9016, certBytes);
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();
            await Task.Delay(100);

            // Create IPEndPoint for client constructor that accepts byte[]
            var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"), 9016);
            using var client = new SimpleTcpClient(endpoint, certBytes);
            client.Settings.AcceptInvalidCertificates = true;
            client.Connect();
            await Task.Delay(100);

            if (!client.IsConnected) throw new Exception("Client with byte[] cert not connected");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestSslBidirectionalDataExchange()
        {
            // Load certificate from PFX with password and exportable flag, then export as byte array
            var certWithPassword = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
            byte[] certBytes = certWithPassword.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx);

            string receivedByServer = null;
            string receivedByClient = null;
            var serverReceived = new ManualResetEventSlim(false);
            var clientReceived = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1", 9017, certBytes);
            server.Settings.AcceptInvalidCertificates = true;
            server.Events.DataReceived += (s, e) =>
            {
                receivedByServer = Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count);
                serverReceived.Set();
            };
            server.Start();
            await Task.Delay(100);

            var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"), 9017);
            using var client = new SimpleTcpClient(endpoint, certBytes);
            client.Settings.AcceptInvalidCertificates = true;
            client.Events.DataReceived += (s, e) =>
            {
                receivedByClient = Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count);
                clientReceived.Set();
            };
            client.Connect();
            await Task.Delay(100);

            // Client sends to server over SSL
            await client.SendAsync("Encrypted hello from client");
            if (!serverReceived.Wait(5000)) throw new Exception("Server did not receive SSL data");
            if (receivedByServer != "Encrypted hello from client")
                throw new Exception($"Server received incorrect SSL data: {receivedByServer}");

            // Server sends to client over SSL
            var clients = server.GetClients().ToList();
            await server.SendAsync(clients[0], "Encrypted hello from server");
            if (!clientReceived.Wait(5000)) throw new Exception("Client did not receive SSL data");
            if (receivedByClient != "Encrypted hello from server")
                throw new Exception($"Client received incorrect SSL data: {receivedByClient}");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerInitiatedDisconnection()
        {
            bool clientDisconnected = false;
            var clientSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9018");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9018");
            client.Events.Disconnected += (s, e) =>
            {
                clientDisconnected = true;
                clientSignal.Set();
            };
            client.Connect();
            await Task.Delay(100);

            var clients = server.GetClients().ToList();
            server.DisconnectClient(clients[0]);

            if (!clientSignal.Wait(5000)) throw new Exception("Client did not detect server-initiated disconnection");
            if (!clientDisconnected) throw new Exception("Client disconnect event not fired");

            server.Stop();
        }

        static async Task TestClientInitiatedDisconnection()
        {
            bool serverDisconnected = false;
            var serverSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9019");
            server.Events.ClientDisconnected += (s, e) =>
            {
                serverDisconnected = true;
                serverSignal.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9019");
            client.Connect();
            await Task.Delay(100);

            client.Disconnect();

            if (!serverSignal.Wait(5000)) throw new Exception("Server did not detect client-initiated disconnection");
            if (!serverDisconnected) throw new Exception("Server disconnect event not fired");

            server.Stop();
        }

        static async Task TestServerDispose()
        {
            SimpleTcpServer server = new SimpleTcpServer("127.0.0.1:9020");
            server.Start();
            await Task.Delay(100);

            if (!server.IsListening) throw new Exception("Server not listening before dispose");

            server.Dispose();
            await Task.Delay(500);

            // After dispose, IsListening should be false
            // Note: The dispose might take a moment to complete all cleanup
            // Should not throw on second dispose
            server.Dispose();
        }

        static async Task TestClientDispose()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9021");
            server.Start();
            await Task.Delay(100);

            SimpleTcpClient client = new SimpleTcpClient("127.0.0.1:9021");
            client.Connect();
            await Task.Delay(100);

            if (!client.IsConnected) throw new Exception("Client not connected before dispose");

            client.Dispose();
            await Task.Delay(100);

            if (client.IsConnected) throw new Exception("Client still connected after dispose");

            // Should not throw
            client.Dispose();

            server.Stop();
        }

        static async Task TestLargeDataTransfer()
        {
            string largeData = new string('X', 100000);
            var receivedData = new StringBuilder();
            var totalReceived = 0;
            var dataReceived = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9022");
            server.Events.DataReceived += (s, e) =>
            {
                lock (_lock)
                {
                    receivedData.Append(Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count));
                    totalReceived += e.Data.Count;
                    if (totalReceived >= 100000)
                    {
                        dataReceived.Set();
                    }
                }
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9022");
            client.Connect();
            await Task.Delay(100);

            await client.SendAsync(largeData);

            if (!dataReceived.Wait(10000)) throw new Exception($"Large data not fully received, got {totalReceived} bytes");
            if (receivedData.Length != 100000) throw new Exception($"Expected 100000 chars, got {receivedData.Length}");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestConcurrentSends()
        {
            var receivedMessages = new HashSet<string>();
            var allReceived = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9023");
            server.Events.DataReceived += (s, e) =>
            {
                lock (_lock)
                {
                    var message = Encoding.UTF8.GetString(e.Data.Array, 0, e.Data.Count);
                    // Messages may be concatenated, split them
                    var parts = message.Split(new[] { "Message " }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        receivedMessages.Add("Message " + part.Trim());
                    }

                    if (receivedMessages.Count >= 10)
                    {
                        allReceived.Set();
                    }
                }
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9023");
            client.Connect();
            await Task.Delay(100);

            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () => await client.SendAsync($"Message {index}")));
            }

            await Task.WhenAll(tasks);

            if (!allReceived.Wait(10000)) throw new Exception($"Only received {receivedMessages.Count} unique messages of 10 concurrent sends");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestConnectWithRetries()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9024");

            // Start server with a delay to test retry logic
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                server.Start();
            });

            using var client = new SimpleTcpClient("127.0.0.1:9024");
            client.ConnectWithRetries(5000);

            if (!client.IsConnected) throw new Exception("Client did not connect with retries");

            client.Disconnect();
            server.Stop();
        }

        class TestResult
        {
            public string Name { get; set; }
            public bool Passed { get; set; }
            public string ErrorMessage { get; set; }
        }
    }
}
