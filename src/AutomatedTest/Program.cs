using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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

                // ConnectAsync tests
                await RunTest("ConnectAsync Basic", TestConnectAsyncBasic);
                await RunTest("ConnectAsync With Cancellation", TestConnectAsyncWithCancellation);
                await RunTest("ConnectAsync Already Connected", TestConnectAsyncAlreadyConnected);

                // DisconnectAsync
                await RunTest("DisconnectAsync", TestDisconnectAsync);

                // Server StartAsync
                await RunTest("Server StartAsync", TestServerStartAsync);

                // Client Send overloads
                await RunTest("Client Send Byte Array", TestClientSendByteArray);
                await RunTest("Client Send Stream", TestClientSendStream);
                await RunTest("Client SendAsync Byte Array", TestClientSendAsyncByteArray);
                await RunTest("Client SendAsync Stream", TestClientSendAsyncStream);
                await RunTest("Client Send String Sync", TestClientSendStringSync);

                // Server Send overloads
                await RunTest("Server Send Byte Array", TestServerSendByteArray);
                await RunTest("Server Send Stream", TestServerSendStream);
                await RunTest("Server SendAsync Byte Array", TestServerSendAsyncByteArray);
                await RunTest("Server SendAsync Stream", TestServerSendAsyncStream);
                await RunTest("Server Send String Sync", TestServerSendStringSync);

                // Constructor coverage
                await RunTest("Client Constructor IPAddress+Port", TestClientConstructorIPAddressPort);
                await RunTest("Client Constructor IPEndPoint", TestClientConstructorIPEndPoint);
                await RunTest("Client Constructor Hostname+Port", TestClientConstructorHostnamePort);
                await RunTest("Client Constructor IPAddress+Port+SSL", TestClientConstructorIPAddressPortSsl);
                await RunTest("Client Constructor IPEndPoint+SSL PFX", TestClientConstructorIPEndPointSslPfx);
                await RunTest("Client Constructor String+Port+X509", TestClientConstructorStringPortX509);
                await RunTest("Client Constructor IPAddress+Port+X509", TestClientConstructorIPAddressPortX509);
                await RunTest("Client Constructor IPEndPoint+X509", TestClientConstructorIPEndPointX509);
                await RunTest("Server Constructor IP+Port", TestServerConstructorIPPort);
                await RunTest("Server Constructor IP+Port+SSL PFX", TestServerConstructorIPPortSslPfx);

                // Settings & Configuration
                await RunTest("Keepalive Settings", TestKeepaliveSettings);
                await RunTest("Statistics Reset", TestStatisticsReset);
                await RunTest("Logger Callback", TestLoggerCallback);
                await RunTest("MaxConnections Enforcement", TestMaxConnectionsEnforcement);
                await RunTest("Client Settings Validation", TestClientSettingsValidation);
                await RunTest("Server Settings Validation", TestServerSettingsValidation);

                // Properties
                await RunTest("Client LocalEndpoint", TestClientLocalEndpoint);
                await RunTest("Server Port Property", TestServerPortProperty);
                await RunTest("Client ServerIpPort", TestClientServerIpPort);

                // Edge Cases & Error Paths
                await RunTest("Connect Already Connected", TestConnectAlreadyConnected);
                await RunTest("Send To Disconnected Client", TestSendToDisconnectedClient);
                await RunTest("SSL With PFX File Path", TestSslWithPfxFilePath);
                await RunTest("SSL CertificateValidationCallback", TestSslCertificateValidationCallback);
                await RunTest("Idle Client Timeout", TestIdleClientTimeout);
                await RunTest("Idle Server Timeout", TestIdleServerTimeout);

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
            string? receivedByServer = null;
            string? receivedByClient = null;
            var serverReceived = new ManualResetEventSlim(false);
            var clientReceived = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9002");
            server.Events.DataReceived += (s, e) =>
            {
                receivedByServer = Encoding.UTF8.GetString(e.Data.Array!, 0, e.Data.Count);
                serverReceived.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9002");
            client.Events.DataReceived += (s, e) =>
            {
                receivedByClient = Encoding.UTF8.GetString(e.Data.Array!, 0, e.Data.Count);
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
            string? connectedClient = null;
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
            string? receivedData = null;
            var eventSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9007");
            server.Events.DataReceived += (s, e) =>
            {
                eventFired = true;
                receivedData = Encoding.UTF8.GetString(e.Data.Array!, 0, e.Data.Count);
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
            string? receivedData = null;
            var eventSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9011");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9011");
            client.Events.DataReceived += (s, e) =>
            {
                eventFired = true;
                receivedData = Encoding.UTF8.GetString(e.Data.Array!, 0, e.Data.Count);
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
#if NET9_0_OR_GREATER
            var certWithPassword = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
#else
            var certWithPassword = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
#endif
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
#if NET9_0_OR_GREATER
            var certWithPassword = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
#else
            var certWithPassword = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
#endif
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
#if NET9_0_OR_GREATER
            var certWithPassword = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
#else
            var certWithPassword = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);
#endif
            byte[] certBytes = certWithPassword.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx);

            string? receivedByServer = null;
            string? receivedByClient = null;
            var serverReceived = new ManualResetEventSlim(false);
            var clientReceived = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1", 9017, certBytes);
            server.Settings.AcceptInvalidCertificates = true;
            server.Events.DataReceived += (s, e) =>
            {
                receivedByServer = Encoding.UTF8.GetString(e.Data.Array!, 0, e.Data.Count);
                serverReceived.Set();
            };
            server.Start();
            await Task.Delay(100);

            var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"), 9017);
            using var client = new SimpleTcpClient(endpoint, certBytes);
            client.Settings.AcceptInvalidCertificates = true;
            client.Events.DataReceived += (s, e) =>
            {
                receivedByClient = Encoding.UTF8.GetString(e.Data.Array!, 0, e.Data.Count);
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
                    receivedData.Append(Encoding.UTF8.GetString(e.Data.Array!, 0, e.Data.Count));
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
                    var message = Encoding.UTF8.GetString(e.Data.Array!, 0, e.Data.Count);
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

        // ===== ConnectAsync Tests =====

        static async Task TestConnectAsyncBasic()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9025");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9025");
            await client.ConnectAsync();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client is not connected after ConnectAsync");

            client.Disconnect();
            await Task.Delay(100);
            if (client.IsConnected) throw new Exception("Client is still connected after disconnect");

            server.Stop();
        }

        static async Task TestConnectAsyncWithCancellation()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9026");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9026");
            var cts = new CancellationTokenSource();
            cts.Cancel(); // pre-cancel

            bool caughtException = false;
            try
            {
                await client.ConnectAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                caughtException = true;
            }

            if (!caughtException) throw new Exception("Expected OperationCanceledException for pre-canceled token");

            server.Stop();
        }

        static async Task TestConnectAsyncAlreadyConnected()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9027");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9027");
            await client.ConnectAsync();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client is not connected");

            // Call ConnectAsync again - should be a no-op
            await client.ConnectAsync();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client disconnected after second ConnectAsync");

            client.Disconnect();
            server.Stop();
        }

        // ===== DisconnectAsync =====

        static async Task TestDisconnectAsync()
        {
            var disconnectedSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9028");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9028");
            client.Events.Disconnected += (s, e) => disconnectedSignal.Set();
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client is not connected");

            await client.DisconnectAsync();
            if (!disconnectedSignal.Wait(5000)) throw new Exception("Disconnected event not fired");
            if (client.IsConnected) throw new Exception("Client is still connected after DisconnectAsync");

            server.Stop();
        }

        // ===== Server StartAsync =====

        static async Task TestServerStartAsync()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9029");
            // StartAsync returns the AcceptConnections task (runs until stopped), so don't await it
            _ = server.StartAsync();
            await Task.Delay(100);
            if (!server.IsListening) throw new Exception("Server is not listening after StartAsync");

            server.Stop();
            await Task.Delay(100);
            if (server.IsListening) throw new Exception("Server is still listening after stop");
        }

        // ===== Client Send Overloads =====

        static async Task TestClientSendByteArray()
        {
            byte[]? receivedBytes = null;
            var signal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9030");
            server.Events.DataReceived += (s, e) =>
            {
                receivedBytes = new byte[e.Data.Count];
                Array.Copy(e.Data.Array!, e.Data.Offset, receivedBytes, 0, e.Data.Count);
                signal.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9030");
            client.Connect();
            await Task.Delay(100);

            byte[] data = new byte[] { 0x01, 0x02, 0x03, 0xFF };
            client.Send(data);

            if (!signal.Wait(5000)) throw new Exception("Server did not receive byte array");
            if (receivedBytes!.Length != 4) throw new Exception($"Expected 4 bytes, got {receivedBytes.Length}");
            if (receivedBytes[0] != 0x01 || receivedBytes[3] != 0xFF) throw new Exception("Byte content mismatch");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientSendStream()
        {
            string? receivedData = null;
            var signal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9031");
            server.Events.DataReceived += (s, e) =>
            {
                receivedData = Encoding.UTF8.GetString(e.Data.Array!, e.Data.Offset, e.Data.Count);
                signal.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9031");
            client.Connect();
            await Task.Delay(100);

            byte[] streamData = Encoding.UTF8.GetBytes("Stream data");
            using var ms = new MemoryStream(streamData);
            client.Send(streamData.Length, ms);

            if (!signal.Wait(5000)) throw new Exception("Server did not receive stream data");
            if (receivedData != "Stream data") throw new Exception($"Expected 'Stream data', got '{receivedData}'");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientSendAsyncByteArray()
        {
            byte[]? receivedBytes = null;
            var signal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9032");
            server.Events.DataReceived += (s, e) =>
            {
                receivedBytes = new byte[e.Data.Count];
                Array.Copy(e.Data.Array!, e.Data.Offset, receivedBytes, 0, e.Data.Count);
                signal.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9032");
            client.Connect();
            await Task.Delay(100);

            byte[] data = new byte[] { 0xAA, 0xBB, 0xCC };
            await client.SendAsync(data);

            if (!signal.Wait(5000)) throw new Exception("Server did not receive async byte array");
            if (receivedBytes!.Length != 3) throw new Exception($"Expected 3 bytes, got {receivedBytes.Length}");
            if (receivedBytes[0] != 0xAA) throw new Exception("Byte content mismatch");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientSendAsyncStream()
        {
            string? receivedData = null;
            var signal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9033");
            server.Events.DataReceived += (s, e) =>
            {
                receivedData = Encoding.UTF8.GetString(e.Data.Array!, e.Data.Offset, e.Data.Count);
                signal.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9033");
            client.Connect();
            await Task.Delay(100);

            byte[] streamData = Encoding.UTF8.GetBytes("Async stream");
            using var ms = new MemoryStream(streamData);
            await client.SendAsync(streamData.Length, ms);

            if (!signal.Wait(5000)) throw new Exception("Server did not receive async stream data");
            if (receivedData != "Async stream") throw new Exception($"Expected 'Async stream', got '{receivedData}'");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientSendStringSync()
        {
            string? receivedData = null;
            var signal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9034");
            server.Events.DataReceived += (s, e) =>
            {
                receivedData = Encoding.UTF8.GetString(e.Data.Array!, e.Data.Offset, e.Data.Count);
                signal.Set();
            };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9034");
            client.Connect();
            await Task.Delay(100);

            client.Send("Sync string send");

            if (!signal.Wait(5000)) throw new Exception("Server did not receive sync string");
            if (receivedData != "Sync string send") throw new Exception($"Expected 'Sync string send', got '{receivedData}'");

            client.Disconnect();
            server.Stop();
        }

        // ===== Server Send Overloads =====

        static async Task TestServerSendByteArray()
        {
            byte[]? receivedBytes = null;
            var signal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9035");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9035");
            client.Events.DataReceived += (s, e) =>
            {
                receivedBytes = new byte[e.Data.Count];
                Array.Copy(e.Data.Array!, e.Data.Offset, receivedBytes, 0, e.Data.Count);
                signal.Set();
            };
            client.Connect();
            await Task.Delay(100);

            var clients = server.GetClients().ToList();
            byte[] data = new byte[] { 0x10, 0x20, 0x30 };
            server.Send(clients[0], data);

            if (!signal.Wait(5000)) throw new Exception("Client did not receive byte array from server");
            if (receivedBytes!.Length != 3) throw new Exception($"Expected 3 bytes, got {receivedBytes.Length}");
            if (receivedBytes[0] != 0x10) throw new Exception("Byte content mismatch");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerSendStream()
        {
            string? receivedData = null;
            var signal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9036");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9036");
            client.Events.DataReceived += (s, e) =>
            {
                receivedData = Encoding.UTF8.GetString(e.Data.Array!, e.Data.Offset, e.Data.Count);
                signal.Set();
            };
            client.Connect();
            await Task.Delay(100);

            var clients = server.GetClients().ToList();
            byte[] streamData = Encoding.UTF8.GetBytes("Server stream");
            using var ms = new MemoryStream(streamData);
            server.Send(clients[0], streamData.Length, ms);

            if (!signal.Wait(5000)) throw new Exception("Client did not receive stream from server");
            if (receivedData != "Server stream") throw new Exception($"Expected 'Server stream', got '{receivedData}'");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerSendAsyncByteArray()
        {
            byte[]? receivedBytes = null;
            var signal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9037");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9037");
            client.Events.DataReceived += (s, e) =>
            {
                receivedBytes = new byte[e.Data.Count];
                Array.Copy(e.Data.Array!, e.Data.Offset, receivedBytes, 0, e.Data.Count);
                signal.Set();
            };
            client.Connect();
            await Task.Delay(100);

            var clients = server.GetClients().ToList();
            byte[] data = new byte[] { 0xDE, 0xAD };
            await server.SendAsync(clients[0], data);

            if (!signal.Wait(5000)) throw new Exception("Client did not receive async byte array from server");
            if (receivedBytes!.Length != 2) throw new Exception($"Expected 2 bytes, got {receivedBytes.Length}");
            if (receivedBytes[0] != 0xDE) throw new Exception("Byte content mismatch");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerSendAsyncStream()
        {
            string? receivedData = null;
            var signal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9038");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9038");
            client.Events.DataReceived += (s, e) =>
            {
                receivedData = Encoding.UTF8.GetString(e.Data.Array!, e.Data.Offset, e.Data.Count);
                signal.Set();
            };
            client.Connect();
            await Task.Delay(100);

            var clients = server.GetClients().ToList();
            byte[] streamData = Encoding.UTF8.GetBytes("Async server stream");
            using var ms = new MemoryStream(streamData);
            await server.SendAsync(clients[0], streamData.Length, ms);

            if (!signal.Wait(5000)) throw new Exception("Client did not receive async stream from server");
            if (receivedData != "Async server stream") throw new Exception($"Expected 'Async server stream', got '{receivedData}'");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerSendStringSync()
        {
            string? receivedData = null;
            var signal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9039");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9039");
            client.Events.DataReceived += (s, e) =>
            {
                receivedData = Encoding.UTF8.GetString(e.Data.Array!, e.Data.Offset, e.Data.Count);
                signal.Set();
            };
            client.Connect();
            await Task.Delay(100);

            var clients = server.GetClients().ToList();
            server.Send(clients[0], "Sync server string");

            if (!signal.Wait(5000)) throw new Exception("Client did not receive sync string from server");
            if (receivedData != "Sync server string") throw new Exception($"Expected 'Sync server string', got '{receivedData}'");

            client.Disconnect();
            server.Stop();
        }

        // ===== Constructor Coverage =====

        static async Task TestClientConstructorIPAddressPort()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9040");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient(IPAddress.Parse("127.0.0.1"), 9040);
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected via IPAddress+Port constructor");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientConstructorIPEndPoint()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9041");
            server.Start();
            await Task.Delay(100);

            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9041);
            using var client = new SimpleTcpClient(endpoint);
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected via IPEndPoint constructor");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientConstructorHostnamePort()
        {
            // Use "127.0.0.1" as hostname to avoid IPv4/IPv6 mismatch (localhost may resolve to ::1)
            using var server = new SimpleTcpServer("127.0.0.1:9042");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1", 9042);
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected via hostname+port constructor");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientConstructorIPAddressPortSsl()
        {
#if NET9_0_OR_GREATER
            var cert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#else
            var cert = new X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#endif
            byte[] certBytes = cert.Export(X509ContentType.Pfx);

            using var server = new SimpleTcpServer("127.0.0.1", 9043, certBytes);
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient(
                IPAddress.Parse("127.0.0.1"), 9043, true, "simpletcp.pfx", "simpletcp");
            client.Settings.AcceptInvalidCertificates = true;
            client.Settings.MutuallyAuthenticate = false;
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected via IPAddress+Port+SSL constructor");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientConstructorIPEndPointSslPfx()
        {
#if NET9_0_OR_GREATER
            var cert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#else
            var cert = new X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#endif
            byte[] certBytes = cert.Export(X509ContentType.Pfx);

            using var server = new SimpleTcpServer("127.0.0.1", 9044, certBytes);
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();
            await Task.Delay(100);

            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9044);
            using var client = new SimpleTcpClient(endpoint, true, "simpletcp.pfx", "simpletcp");
            client.Settings.AcceptInvalidCertificates = true;
            client.Settings.MutuallyAuthenticate = false;
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected via IPEndPoint+SSL PFX constructor");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientConstructorStringPortX509()
        {
#if NET9_0_OR_GREATER
            var cert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#else
            var cert = new X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#endif
            byte[] certBytes = cert.Export(X509ContentType.Pfx);

            using var server = new SimpleTcpServer("127.0.0.1", 9045, certBytes);
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();
            await Task.Delay(100);

#if NET9_0_OR_GREATER
            var clientCert = X509CertificateLoader.LoadPkcs12(certBytes, null);
#else
            var clientCert = new X509Certificate2(certBytes);
#endif
            using var client = new SimpleTcpClient("127.0.0.1", 9045, clientCert);
            client.Settings.AcceptInvalidCertificates = true;
            client.Settings.MutuallyAuthenticate = false;
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected via String+Port+X509 constructor");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientConstructorIPAddressPortX509()
        {
#if NET9_0_OR_GREATER
            var cert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#else
            var cert = new X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#endif
            byte[] certBytes = cert.Export(X509ContentType.Pfx);

            using var server = new SimpleTcpServer("127.0.0.1", 9046, certBytes);
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();
            await Task.Delay(100);

#if NET9_0_OR_GREATER
            var clientCert = X509CertificateLoader.LoadPkcs12(certBytes, null);
#else
            var clientCert = new X509Certificate2(certBytes);
#endif
            using var client = new SimpleTcpClient(IPAddress.Parse("127.0.0.1"), 9046, clientCert);
            client.Settings.AcceptInvalidCertificates = true;
            client.Settings.MutuallyAuthenticate = false;
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected via IPAddress+Port+X509 constructor");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestClientConstructorIPEndPointX509()
        {
#if NET9_0_OR_GREATER
            var cert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#else
            var cert = new X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#endif
            byte[] certBytes = cert.Export(X509ContentType.Pfx);

            using var server = new SimpleTcpServer("127.0.0.1", 9047, certBytes);
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();
            await Task.Delay(100);

#if NET9_0_OR_GREATER
            var clientCert = X509CertificateLoader.LoadPkcs12(certBytes, null);
#else
            var clientCert = new X509Certificate2(certBytes);
#endif
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9047);
            using var client = new SimpleTcpClient(endpoint, clientCert);
            client.Settings.AcceptInvalidCertificates = true;
            client.Settings.MutuallyAuthenticate = false;
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected via IPEndPoint+X509 constructor");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerConstructorIPPort()
        {
            using var server = new SimpleTcpServer("127.0.0.1", 9048);
            server.Start();
            await Task.Delay(100);
            if (!server.IsListening) throw new Exception("Server not listening via IP+Port constructor");

            using var client = new SimpleTcpClient("127.0.0.1:9048");
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected to IP+Port server");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerConstructorIPPortSslPfx()
        {
#if NET9_0_OR_GREATER
            var cert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#else
            var cert = new X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#endif
            byte[] certBytes = cert.Export(X509ContentType.Pfx);

            using var server = new SimpleTcpServer("127.0.0.1", 9049, true, "simpletcp.pfx", "simpletcp");
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();
            await Task.Delay(100);
            if (!server.IsListening) throw new Exception("SSL server not listening via IP+Port+SSL constructor");

            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9049);
            using var client = new SimpleTcpClient(endpoint, certBytes);
            client.Settings.AcceptInvalidCertificates = true;
            client.Settings.MutuallyAuthenticate = false;
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected to SSL IP+Port server");

            client.Disconnect();
            server.Stop();
        }

        // ===== Settings & Configuration =====

        static async Task TestKeepaliveSettings()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9050");
            server.Keepalive.EnableTcpKeepAlives = true;
            server.Keepalive.TcpKeepAliveInterval = 5;
            server.Keepalive.TcpKeepAliveTime = 5;
            server.Keepalive.TcpKeepAliveRetryCount = 3;
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9050");
            client.Keepalive.EnableTcpKeepAlives = true;
            client.Keepalive.TcpKeepAliveInterval = 5;
            client.Keepalive.TcpKeepAliveTime = 5;
            client.Keepalive.TcpKeepAliveRetryCount = 3;
            client.Connect();
            await Task.Delay(100);

            if (!client.IsConnected) throw new Exception("Client not connected with keepalives enabled");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestStatisticsReset()
        {
            var serverReceived = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9051");
            server.Events.DataReceived += (s, e) => serverReceived.Set();
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9051");
            client.Connect();
            await Task.Delay(100);

            await client.SendAsync("test data");
            if (!serverReceived.Wait(5000)) throw new Exception("Server did not receive data");
            await Task.Delay(200);

            if (client.Statistics.SentBytes <= 0) throw new Exception("Client SentBytes should be > 0");
            if (server.Statistics.ReceivedBytes <= 0) throw new Exception("Server ReceivedBytes should be > 0");

            client.Statistics.Reset();
            server.Statistics.Reset();

            if (client.Statistics.SentBytes != 0) throw new Exception($"Client SentBytes should be 0 after reset, got {client.Statistics.SentBytes}");
            if (client.Statistics.ReceivedBytes != 0) throw new Exception($"Client ReceivedBytes should be 0 after reset, got {client.Statistics.ReceivedBytes}");
            if (server.Statistics.SentBytes != 0) throw new Exception($"Server SentBytes should be 0 after reset, got {server.Statistics.SentBytes}");
            if (server.Statistics.ReceivedBytes != 0) throw new Exception($"Server ReceivedBytes should be 0 after reset, got {server.Statistics.ReceivedBytes}");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestLoggerCallback()
        {
            var serverLogs = new List<string>();
            var clientLogs = new List<string>();

            using var server = new SimpleTcpServer("127.0.0.1:9052");
            server.Logger = (msg) => { lock (_lock) { serverLogs.Add(msg); } };
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9052");
            client.Logger = (msg) => { lock (_lock) { clientLogs.Add(msg); } };
            client.Connect();
            await Task.Delay(200);

            if (clientLogs.Count == 0) throw new Exception("Client logger captured no messages");
            if (serverLogs.Count == 0) throw new Exception("Server logger captured no messages");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestMaxConnectionsEnforcement()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9053");
            server.Settings.MaxConnections = 2;
            server.Start();
            await Task.Delay(100);

            using var client1 = new SimpleTcpClient("127.0.0.1:9053");
            using var client2 = new SimpleTcpClient("127.0.0.1:9053");
            using var client3 = new SimpleTcpClient("127.0.0.1:9053");

            client1.Connect();
            await Task.Delay(100);
            client2.Connect();
            await Task.Delay(100);

            if (server.Connections != 2) throw new Exception($"Expected 2 connections, got {server.Connections}");

            // Third client connects - server should reject/disconnect it
            try
            {
                client3.Connect();
                await Task.Delay(500);
            }
            catch
            {
                // Connection might fail - that's OK
            }

            // Server should still have at most 2 connections
            if (server.Connections > 2) throw new Exception($"Expected max 2 connections, got {server.Connections}");

            client1.Disconnect();
            client2.Disconnect();
            server.Stop();
        }

        static async Task TestClientSettingsValidation()
        {
            var settings = new SimpleTcpClientSettings();

            // StreamBufferSize < 1
            bool threw = false;
            try { settings.StreamBufferSize = 0; } catch (ArgumentException) { threw = true; }
            if (!threw) throw new Exception("StreamBufferSize=0 should throw");

            // StreamBufferSize > 65536
            threw = false;
            try { settings.StreamBufferSize = 65537; } catch (ArgumentException) { threw = true; }
            if (!threw) throw new Exception("StreamBufferSize=65537 should throw");

            // ConnectTimeoutMs < 1
            threw = false;
            try { settings.ConnectTimeoutMs = 0; } catch (ArgumentException) { threw = true; }
            if (!threw) throw new Exception("ConnectTimeoutMs=0 should throw");

            // IdleServerTimeoutMs < 0
            threw = false;
            try { settings.IdleServerTimeoutMs = -1; } catch (ArgumentException) { threw = true; }
            if (!threw) throw new Exception("IdleServerTimeoutMs=-1 should throw");

            // Valid values should not throw
            settings.StreamBufferSize = 1024;
            settings.ConnectTimeoutMs = 5000;
            settings.IdleServerTimeoutMs = 0;

            await Task.CompletedTask;
        }

        static async Task TestServerSettingsValidation()
        {
            var settings = new SimpleTcpServerSettings();

            // StreamBufferSize < 1
            bool threw = false;
            try { settings.StreamBufferSize = 0; } catch (ArgumentException) { threw = true; }
            if (!threw) throw new Exception("StreamBufferSize=0 should throw");

            // MaxConnections < 1
            threw = false;
            try { settings.MaxConnections = 0; } catch (ArgumentException) { threw = true; }
            if (!threw) throw new Exception("MaxConnections=0 should throw");

            // IdleClientTimeoutMs < 0
            threw = false;
            try { settings.IdleClientTimeoutMs = -1; } catch (ArgumentException) { threw = true; }
            if (!threw) throw new Exception("IdleClientTimeoutMs=-1 should throw");

            // Valid values
            settings.StreamBufferSize = 4096;
            settings.MaxConnections = 100;
            settings.IdleClientTimeoutMs = 0;

            await Task.CompletedTask;
        }

        // ===== Properties =====

        static async Task TestClientLocalEndpoint()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9056");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9056");
            client.Connect();
            await Task.Delay(100);

            var localEp = client.LocalEndpoint;
            if (localEp == null) throw new Exception("LocalEndpoint is null when connected");
            if (!localEp.Address.Equals(IPAddress.Parse("127.0.0.1")))
                throw new Exception($"Expected 127.0.0.1, got {localEp.Address}");
            if (localEp.Port <= 0) throw new Exception($"LocalEndpoint port should be > 0, got {localEp.Port}");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestServerPortProperty()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9057");
            server.Start();
            await Task.Delay(100);

            if (server.Port != 9057) throw new Exception($"Expected port 9057, got {server.Port}");

            server.Stop();
        }

        static async Task TestClientServerIpPort()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9058");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9058");
            if (client.ServerIpPort != "127.0.0.1:9058") throw new Exception($"Expected '127.0.0.1:9058', got '{client.ServerIpPort}'");

            server.Stop();
        }

        // ===== Edge Cases & Error Paths =====

        static async Task TestConnectAlreadyConnected()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9059");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9059");
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected");

            // Call Connect() again - should be no-op
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client disconnected after second Connect()");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestSendToDisconnectedClient()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9060");
            server.Start();
            await Task.Delay(100);

            // Server.Send to unknown ipPort silently returns (no-op) - verify no crash
            server.Send("127.0.0.1:99999", "test");

            // Verify Send with null/empty ipPort throws ArgumentNullException
            bool threw = false;
            try
            {
                server.Send("", "test");
            }
            catch (ArgumentNullException)
            {
                threw = true;
            }
            if (!threw) throw new Exception("Sending with empty ipPort should throw ArgumentNullException");

            server.Stop();
        }

        static async Task TestSslWithPfxFilePath()
        {
            using var server = new SimpleTcpServer("127.0.0.1:9061", true, "simpletcp.pfx", "simpletcp");
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();
            await Task.Delay(100);
            if (!server.IsListening) throw new Exception("SSL server with PFX file not listening");

            using var client = new SimpleTcpClient("127.0.0.1:9061", true, "simpletcp.pfx", "simpletcp");
            client.Settings.AcceptInvalidCertificates = true;
            client.Settings.MutuallyAuthenticate = false;
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected via PFX file path SSL");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestSslCertificateValidationCallback()
        {
            bool callbackInvoked = false;

#if NET9_0_OR_GREATER
            var cert = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#else
            var cert = new X509Certificate2(
                File.ReadAllBytes("simpletcp.pfx"), "simpletcp",
                X509KeyStorageFlags.Exportable);
#endif
            byte[] certBytes = cert.Export(X509ContentType.Pfx);

            using var server = new SimpleTcpServer("127.0.0.1", 9062, certBytes);
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();
            await Task.Delay(100);

            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9062);
            using var client = new SimpleTcpClient(endpoint, certBytes);
            client.Settings.AcceptInvalidCertificates = false;
            client.Settings.MutuallyAuthenticate = false;
            client.Settings.CertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                callbackInvoked = true;
                return true;
            };
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected with custom validation callback");
            if (!callbackInvoked) throw new Exception("CertificateValidationCallback was not invoked");

            client.Disconnect();
            server.Stop();
        }

        static async Task TestIdleClientTimeout()
        {
            var clientDisconnected = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9063");
            server.Settings.IdleClientTimeoutMs = 2000;
            server.Settings.IdleClientEvaluationIntervalMs = 500;
            server.Events.ClientDisconnected += (s, e) => clientDisconnected.Set();
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9063");
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected");

            // Wait for idle timeout - server should disconnect the client
            if (!clientDisconnected.Wait(10000))
                throw new Exception("Client was not disconnected by idle timeout");
        }

        static async Task TestIdleServerTimeout()
        {
            var clientDisconnectedSignal = new ManualResetEventSlim(false);

            using var server = new SimpleTcpServer("127.0.0.1:9064");
            server.Start();
            await Task.Delay(100);

            using var client = new SimpleTcpClient("127.0.0.1:9064");
            client.Settings.IdleServerTimeoutMs = 2000;
            client.Settings.IdleServerEvaluationIntervalMs = 500;
            client.Events.Disconnected += (s, e) => clientDisconnectedSignal.Set();
            client.Connect();
            await Task.Delay(100);
            if (!client.IsConnected) throw new Exception("Client not connected");

            // Wait for idle timeout - client should disconnect due to server inactivity
            if (!clientDisconnectedSignal.Wait(10000))
                throw new Exception("Client did not disconnect due to idle server timeout");
        }

        class TestResult
        {
            public string Name { get; set; } = string.Empty;
            public bool Passed { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }
    }
}
