namespace TestPerformanceBenchmark
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using SuperSimpleTcp;

    internal static class Program
    {
        private const string Host = "127.0.0.1";
        private const string CertificatePassword = "simpletcp";
        private const int FrameHeaderLength = 4;
        private const int MinBenchmarkPort = 20000;
        private const int MaxBenchmarkPort = 60000;
        private const int MaxPortSelectionAttempts = 32;

        private static readonly object PayloadLock = new object();
        private static readonly Dictionary<int, byte[]> PayloadCache = new Dictionary<int, byte[]>();
        private static readonly BenchmarkTransportMode[] Transports =
        {
            BenchmarkTransportMode.Tcp,
            BenchmarkTransportMode.Ssl
        };
        private static readonly PayloadBenchmarkCase[] FullPayloadCases =
        {
            new PayloadBenchmarkCase("64B", 64, 250, 2000, new[] { 1, 4 }, 30000, 120000),
            new PayloadBenchmarkCase("64KB", 64 * 1024, 40, 96, new[] { 1, 4 }, 90000, 180000),
            new PayloadBenchmarkCase("64MB", 64 * 1024 * 1024, 5, 4, new[] { 1, 2 }, 300000, 900000)
        };
        private static readonly PayloadBenchmarkCase[] QuickPayloadCases =
        {
            new PayloadBenchmarkCase("64B", 64, 25, 200, new[] { 1, 2 }, 15000, 30000),
            new PayloadBenchmarkCase("64KB", 64 * 1024, 8, 16, new[] { 1, 2 }, 30000, 60000),
            new PayloadBenchmarkCase("1MB", 1024 * 1024, 3, 2, new[] { 1 }, 60000, 120000)
        };
        private static readonly ConnectionSetupCase[] FullSetupCases =
        {
            new ConnectionSetupCase("Sequential", 1, 20),
            new ConnectionSetupCase("Burst x4", 4, 10)
        };
        private static readonly ConnectionSetupCase[] QuickSetupCases =
        {
            new ConnectionSetupCase("Sequential", 1, 4),
            new ConnectionSetupCase("Burst x4", 4, 2)
        };

        private static BenchmarkCommandLineOptions _options = new BenchmarkCommandLineOptions();

        private static int Main(string[] args)
        {
            try
            {
                _options = BenchmarkCommandLineOptions.Parse(args);
                RunAsync().GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Fatal error:");
                Console.Error.WriteLine(e.ToString());
                return 1;
            }
        }

        private static async Task RunAsync()
        {
            PayloadBenchmarkCase[] payloadCases = _options.Quick ? QuickPayloadCases : FullPayloadCases;
            ConnectionSetupCase[] setupCases = _options.Quick ? QuickSetupCases : FullSetupCases;

            PrintHeader(payloadCases, setupCases);
            PrintDetailedSummary(payloadCases, setupCases);

            DateTimeOffset runStarted = DateTimeOffset.Now;
            Stopwatch stopwatch = Stopwatch.StartNew();

            List<LatencyBenchmarkResult> latencyResults = await RunResponseTimeSuiteAsync(payloadCases).ConfigureAwait(false);
            List<ThroughputBenchmarkResult> throughputResults = await RunThroughputSuiteAsync(payloadCases).ConfigureAwait(false);
            List<ConnectionSetupBenchmarkResult> connectionSetupResults = await RunConnectionSetupSuiteAsync(setupCases).ConfigureAwait(false);

            stopwatch.Stop();
            DateTimeOffset runCompleted = DateTimeOffset.Now;

            PrintFinalSummary(
                runStarted,
                runCompleted,
                stopwatch.Elapsed,
                latencyResults,
                throughputResults,
                connectionSetupResults);
        }

        private static void PrintHeader(PayloadBenchmarkCase[] payloadCases, ConnectionSetupCase[] setupCases)
        {
            Console.WriteLine("SuperSimpleTcp Performance Benchmark");
            Console.WriteLine("Host         : " + Host);
            Console.WriteLine("Transports   : " + String.Join(", ", Transports.Select(mode => mode.ToString())));
            Console.WriteLine("Payload Cases: " + String.Join(", ", payloadCases.Select(c => c.Name)));
            Console.WriteLine("Setup Cases  : " + String.Join(", ", setupCases.Select(c => c.Name)));
            Console.WriteLine("Run Mode     : " + (_options.Quick ? "Quick" : "Full"));
            Console.WriteLine("Output Mode  : " + (_options.SummaryOnly ? "Summary Only" : "Detailed"));
            Console.WriteLine();
        }

        private static void PrintDetailedSummary(PayloadBenchmarkCase[] payloadCases, ConnectionSetupCase[] setupCases)
        {
            WriteDetail("This harness measures loopback response time, throughput, and connection setup.");
            WriteDetail("SuperSimpleTcp surfaces TCP read chunks, so the benchmark uses an internal 4-byte length prefix");
            WriteDetail("to reconstruct logical messages without changing the public API.");
            WriteDetail();
            WriteDetail("Response Time Cases");
            foreach (PayloadBenchmarkCase payloadCase in payloadCases)
            {
                WriteDetail(
                    "  " + payloadCase.Name +
                    " | iterations " + payloadCase.LatencyIterations +
                    " | timeout " + payloadCase.ResponseTimeoutMs + " ms");
            }

            WriteDetail();
            WriteDetail("Throughput Cases");
            foreach (PayloadBenchmarkCase payloadCase in payloadCases)
            {
                WriteDetail(
                    "  " + payloadCase.Name +
                    " | messages/client " + payloadCase.ThroughputMessagesPerClient +
                    " | clients " + String.Join(", ", payloadCase.ThroughputClientCounts) +
                    " | timeout " + payloadCase.ThroughputTimeoutMs + " ms");
            }

            WriteDetail();
            WriteDetail("Connection Setup Cases");
            foreach (ConnectionSetupCase setupCase in setupCases)
            {
                WriteDetail(
                    "  " + setupCase.Name +
                    " | batch size " + setupCase.BatchSize +
                    " | batches " + setupCase.BatchIterations);
            }

            WriteDetail();
        }

        private static async Task<List<LatencyBenchmarkResult>> RunResponseTimeSuiteAsync(PayloadBenchmarkCase[] payloadCases)
        {
            WriteDetail("=== Response Time Suite ===");
            WriteDetail("Method: client SendAsync(stream) -> server framed ack -> client wait for framed response");
            WriteDetail();

            List<LatencyBenchmarkResult> results = new List<LatencyBenchmarkResult>();

            foreach (BenchmarkTransportMode transport in Transports)
            {
                foreach (PayloadBenchmarkCase payloadCase in payloadCases)
                {
                    results.Add(await RunResponseTimeCaseAsync(transport, payloadCase).ConfigureAwait(false));
                }
            }

            return results;
        }

        private static async Task<LatencyBenchmarkResult> RunResponseTimeCaseAsync(BenchmarkTransportMode transport, PayloadBenchmarkCase payloadCase)
        {
            ForceCollection();

            WriteDetail(
                "Case: " + transport +
                " / " + payloadCase.Name +
                " / " + payloadCase.LatencyIterations + " measured iterations");

            int port = GetAvailablePort();
            byte[] payload = GetPayload(payloadCase.SizeBytes);
            List<double> samples = new List<double>(payloadCase.LatencyIterations);
            ConcurrentDictionary<string, FrameAccumulator> serverFrames = new ConcurrentDictionary<string, FrameAccumulator>();
            AcknowledgementMailbox mailbox = new AcknowledgementMailbox();

            SimpleTcpServer server = null;
            SimpleTcpClient client = null;

            try
            {
                server = CreateServer(transport, port);
                server.Events.DataReceived += (_, args) =>
                {
                    try
                    {
                        FrameAccumulator accumulator = serverFrames.GetOrAdd(args.IpPort, _ => new FrameAccumulator());
                        accumulator.Append(
                            args.Data,
                            (buffer, offset, count) =>
                            {
                                server.Send(args.IpPort, CreateAcknowledgementFrame(count));
                            });
                    }
                    catch (Exception e)
                    {
                        mailbox.Fail(e);
                        throw;
                    }
                };

                client = CreateClient(transport, port);
                client.Events.DataReceived += (_, args) =>
                {
                    try
                    {
                        mailbox.Append(args.Data);
                    }
                    catch (Exception e)
                    {
                        mailbox.Fail(e);
                        throw;
                    }
                };

                server.Start();
                await WaitForConditionAsync(
                    () => server.IsListening,
                    TimeSpan.FromSeconds(2),
                    "Server did not start listening.").ConfigureAwait(false);

                client.Connect();
                await WaitForConditionAsync(
                    () => client.IsConnected && server.Connections == 1,
                    TimeSpan.FromSeconds(5),
                    "Client did not connect to the server.").ConfigureAwait(false);

                int warmupIterations = Math.Min(3, payloadCase.LatencyIterations);
                for (int i = 0; i < warmupIterations; i++)
                {
                    int ackLength = await SendLatencyProbeAsync(client, payload, mailbox, payloadCase.ResponseTimeoutMs).ConfigureAwait(false);
                    if (ackLength != payloadCase.SizeBytes)
                    {
                        throw new InvalidOperationException(
                            "Warmup acknowledgement mismatch. Expected " + payloadCase.SizeBytes + ", received " + ackLength + ".");
                    }
                }

                for (int i = 0; i < payloadCase.LatencyIterations; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    int ackLength = await SendLatencyProbeAsync(client, payload, mailbox, payloadCase.ResponseTimeoutMs).ConfigureAwait(false);
                    double elapsedMs = StopwatchTicksToMilliseconds(Stopwatch.GetTimestamp() - start);

                    if (ackLength != payloadCase.SizeBytes)
                    {
                        throw new InvalidOperationException(
                            "Latency acknowledgement mismatch. Expected " + payloadCase.SizeBytes + ", received " + ackLength + ".");
                    }

                    samples.Add(elapsedMs);
                }
            }
            finally
            {
                SafeCleanupClient(client);
                SafeCleanupServer(server);
            }

            StatisticsSummary summary = StatisticsSummary.From(samples);

            WriteDetail(
                "  Min " + summary.Min.ToString("N3") + " ms" +
                " | Avg " + summary.Average.ToString("N3") + " ms" +
                " | P50 " + summary.P50.ToString("N3") + " ms" +
                " | P95 " + summary.P95.ToString("N3") + " ms" +
                " | P99 " + summary.P99.ToString("N3") + " ms" +
                " | Max " + summary.Max.ToString("N3") + " ms");
            WriteDetail();

            return new LatencyBenchmarkResult(transport, payloadCase.Name, payloadCase.LatencyIterations, summary);
        }

        private static async Task<int> SendLatencyProbeAsync(
            SimpleTcpClient client,
            byte[] payload,
            AcknowledgementMailbox mailbox,
            int responseTimeoutMs)
        {
            Task<int> acknowledgementTask = mailbox.ExpectAcknowledgement();

            using (FramedPayloadStream stream = new FramedPayloadStream(payload))
            {
                await client.SendAsync(stream.Length, stream).ConfigureAwait(false);
            }

            return await WithTimeoutAsync(
                acknowledgementTask,
                TimeSpan.FromMilliseconds(responseTimeoutMs),
                "Timed out waiting for acknowledgement from the server.").ConfigureAwait(false);
        }

        private static async Task<List<ThroughputBenchmarkResult>> RunThroughputSuiteAsync(PayloadBenchmarkCase[] payloadCases)
        {
            WriteDetail("=== Throughput Suite ===");
            WriteDetail("Method: concurrent client SendAsync(stream) -> server framed message accounting");
            WriteDetail();

            List<ThroughputBenchmarkResult> results = new List<ThroughputBenchmarkResult>();

            foreach (BenchmarkTransportMode transport in Transports)
            {
                foreach (PayloadBenchmarkCase payloadCase in payloadCases)
                {
                    foreach (int clientCount in payloadCase.ThroughputClientCounts)
                    {
                        results.Add(await RunThroughputCaseAsync(transport, payloadCase, clientCount).ConfigureAwait(false));
                    }
                }
            }

            return results;
        }

        private static async Task<ThroughputBenchmarkResult> RunThroughputCaseAsync(
            BenchmarkTransportMode transport,
            PayloadBenchmarkCase payloadCase,
            int clientCount)
        {
            ForceCollection();

            int messageCount = payloadCase.ThroughputMessagesPerClient;
            int expectedMessages = clientCount * messageCount;
            long expectedBytes = (long)payloadCase.SizeBytes * (long)expectedMessages;

            WriteDetail(
                "Case: " + transport +
                " / " + payloadCase.Name +
                " / clients " + clientCount +
                " / messages per client " + messageCount +
                " / total bytes " + FormatBytes(expectedBytes));

            int port = GetAvailablePort();
            byte[] payload = GetPayload(payloadCase.SizeBytes);
            long receivedBytes = 0;
            int receivedMessages = 0;
            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ConcurrentDictionary<string, FrameAccumulator> serverFrames = new ConcurrentDictionary<string, FrameAccumulator>();

            SimpleTcpServer server = null;

            try
            {
                server = CreateServer(transport, port);
                server.Events.DataReceived += (_, args) =>
                {
                    try
                    {
                        FrameAccumulator accumulator = serverFrames.GetOrAdd(args.IpPort, _ => new FrameAccumulator());
                        accumulator.Append(
                            args.Data,
                            (_, _, count) =>
                            {
                                Interlocked.Add(ref receivedBytes, count);
                                if (Interlocked.Increment(ref receivedMessages) == expectedMessages)
                                {
                                    completion.TrySetResult(true);
                                }
                            });
                    }
                    catch (Exception e)
                    {
                        completion.TrySetException(e);
                        throw;
                    }
                };

                server.Start();
                await WaitForConditionAsync(
                    () => server.IsListening,
                    TimeSpan.FromSeconds(2),
                    "Server did not start listening.").ConfigureAwait(false);

                List<SimpleTcpClient> clients = new List<SimpleTcpClient>();

                try
                {
                    for (int i = 0; i < clientCount; i++)
                    {
                        SimpleTcpClient client = CreateClient(transport, port);
                        client.Connect();
                        clients.Add(client);
                    }

                    await WaitForConditionAsync(
                        () => clients.All(c => c.IsConnected) && server.Connections == clientCount,
                        TimeSpan.FromSeconds(10),
                        "Not all benchmark clients connected in time.").ConfigureAwait(false);

                    long suiteStart = Stopwatch.GetTimestamp();
                    List<Task> sendTasks = new List<Task>(clientCount);

                    foreach (SimpleTcpClient client in clients)
                    {
                        sendTasks.Add(SendPayloadBurstAsync(client, payload, messageCount));
                    }

                    await Task.WhenAll(sendTasks).ConfigureAwait(false);
                    double sendCompletionMs = StopwatchTicksToMilliseconds(Stopwatch.GetTimestamp() - suiteStart);

                    await WithTimeoutAsync(
                        completion.Task,
                        TimeSpan.FromMilliseconds(payloadCase.ThroughputTimeoutMs),
                        "Timed out waiting for the throughput case to finish.").ConfigureAwait(false);

                    double totalCompletionMs = StopwatchTicksToMilliseconds(Stopwatch.GetTimestamp() - suiteStart);

                    if (receivedMessages != expectedMessages)
                    {
                        throw new InvalidOperationException(
                            "Expected " + expectedMessages + " messages but received " + receivedMessages + ".");
                    }

                    if (receivedBytes != expectedBytes)
                    {
                        throw new InvalidOperationException(
                            "Expected " + expectedBytes + " bytes but received " + receivedBytes + ".");
                    }

                    double totalSeconds = totalCompletionMs / 1000d;
                    if (totalSeconds <= 0) totalSeconds = 0.001d;

                    double messagesPerSecond = expectedMessages / totalSeconds;
                    double mebibytesPerSecond = (expectedBytes / 1024d / 1024d) / totalSeconds;

                    WriteDetail(
                        "  Send completion " + sendCompletionMs.ToString("N2") + " ms" +
                        " | End-to-end completion " + totalCompletionMs.ToString("N2") + " ms");
                    WriteDetail(
                        "  Throughput " + messagesPerSecond.ToString("N2") + " msg/s" +
                        " | " + mebibytesPerSecond.ToString("N2") + " MiB/s");
                    WriteDetail();

                    return new ThroughputBenchmarkResult(
                        transport,
                        payloadCase.Name,
                        clientCount,
                        messageCount,
                        expectedBytes,
                        sendCompletionMs,
                        totalCompletionMs,
                        messagesPerSecond,
                        mebibytesPerSecond);
                }
                finally
                {
                    foreach (SimpleTcpClient client in clients)
                    {
                        SafeCleanupClient(client);
                    }
                }
            }
            finally
            {
                SafeCleanupServer(server);
            }
        }

        private static async Task SendPayloadBurstAsync(SimpleTcpClient client, byte[] payload, int messageCount)
        {
            for (int i = 0; i < messageCount; i++)
            {
                using (FramedPayloadStream stream = new FramedPayloadStream(payload))
                {
                    await client.SendAsync(stream.Length, stream).ConfigureAwait(false);
                }
            }
        }

        private static async Task<List<ConnectionSetupBenchmarkResult>> RunConnectionSetupSuiteAsync(ConnectionSetupCase[] setupCases)
        {
            WriteDetail("=== Connection Setup Suite ===");
            WriteDetail("Method: repeated connect/disconnect cycles against a live loopback server");
            WriteDetail();

            List<ConnectionSetupBenchmarkResult> results = new List<ConnectionSetupBenchmarkResult>();

            foreach (BenchmarkTransportMode transport in Transports)
            {
                foreach (ConnectionSetupCase setupCase in setupCases)
                {
                    results.Add(await RunConnectionSetupCaseAsync(transport, setupCase).ConfigureAwait(false));
                }
            }

            return results;
        }

        private static async Task<ConnectionSetupBenchmarkResult> RunConnectionSetupCaseAsync(
            BenchmarkTransportMode transport,
            ConnectionSetupCase setupCase)
        {
            ForceCollection();

            WriteDetail(
                "Case: " + transport +
                " / " + setupCase.Name +
                " / batch size " + setupCase.BatchSize +
                " / batches " + setupCase.BatchIterations);

            int port = GetAvailablePort();
            List<double> connectionSamples = new List<double>(setupCase.BatchSize * setupCase.BatchIterations);
            List<double> batchSamples = new List<double>(setupCase.BatchIterations);

            SimpleTcpServer server = null;

            try
            {
                server = CreateServer(transport, port);
                server.Start();
                await WaitForConditionAsync(
                    () => server.IsListening,
                    TimeSpan.FromSeconds(2),
                    "Server did not start listening.").ConfigureAwait(false);

                for (int batchIndex = 0; batchIndex < setupCase.BatchIterations; batchIndex++)
                {
                    List<ConnectionMeasurement> measurements = new List<ConnectionMeasurement>(setupCase.BatchSize);
                    long batchStart = Stopwatch.GetTimestamp();

                    try
                    {
                        List<Task<ConnectionMeasurement>> tasks = new List<Task<ConnectionMeasurement>>(setupCase.BatchSize);
                        for (int i = 0; i < setupCase.BatchSize; i++)
                        {
                            tasks.Add(Task.Run(() => ConnectClient(transport, port)));
                        }

                        ConnectionMeasurement[] batchMeasurements = await Task.WhenAll(tasks).ConfigureAwait(false);
                        measurements.AddRange(batchMeasurements);

                        foreach (ConnectionMeasurement measurement in batchMeasurements)
                        {
                            connectionSamples.Add(measurement.ElapsedMilliseconds);
                        }
                    }
                    finally
                    {
                        foreach (ConnectionMeasurement measurement in measurements)
                        {
                            SafeCleanupClient(measurement.Client);
                        }
                    }

                    double batchElapsedMs = StopwatchTicksToMilliseconds(Stopwatch.GetTimestamp() - batchStart);
                    batchSamples.Add(batchElapsedMs);
                }
            }
            finally
            {
                SafeCleanupServer(server);
            }

            StatisticsSummary perConnectionSummary = StatisticsSummary.From(connectionSamples);
            StatisticsSummary perBatchSummary = StatisticsSummary.From(batchSamples);

            WriteDetail(
                "  Per-connection: Min " + perConnectionSummary.Min.ToString("N3") + " ms" +
                " | Avg " + perConnectionSummary.Average.ToString("N3") + " ms" +
                " | P50 " + perConnectionSummary.P50.ToString("N3") + " ms" +
                " | P95 " + perConnectionSummary.P95.ToString("N3") + " ms" +
                " | Max " + perConnectionSummary.Max.ToString("N3") + " ms");
            WriteDetail(
                "  Per-batch     : Min " + perBatchSummary.Min.ToString("N3") + " ms" +
                " | Avg " + perBatchSummary.Average.ToString("N3") + " ms" +
                " | P50 " + perBatchSummary.P50.ToString("N3") + " ms" +
                " | P95 " + perBatchSummary.P95.ToString("N3") + " ms" +
                " | Max " + perBatchSummary.Max.ToString("N3") + " ms");
            WriteDetail();

            return new ConnectionSetupBenchmarkResult(
                transport,
                setupCase.Name,
                setupCase.BatchSize,
                setupCase.BatchIterations,
                perConnectionSummary,
                perBatchSummary);
        }

        private static ConnectionMeasurement ConnectClient(BenchmarkTransportMode transport, int port)
        {
            SimpleTcpClient client = CreateClient(transport, port);

            long start = Stopwatch.GetTimestamp();
            client.Connect();
            double elapsedMs = StopwatchTicksToMilliseconds(Stopwatch.GetTimestamp() - start);

            return new ConnectionMeasurement(client, elapsedMs);
        }

        private static SimpleTcpServer CreateServer(BenchmarkTransportMode transport, int port)
        {
            SimpleTcpServer server;

            if (transport == BenchmarkTransportMode.Tcp)
            {
                server = new SimpleTcpServer(Host, port);
            }
            else
            {
                server = new SimpleTcpServer(Host, port, true, GetCertificatePath(), CertificatePassword);
                server.Settings.AcceptInvalidCertificates = true;
                server.Settings.MutuallyAuthenticate = false;
            }

            server.Settings.NoDelay = true;
            server.Settings.UseAsyncDataReceivedEvents = false;
            return server;
        }

        private static SimpleTcpClient CreateClient(BenchmarkTransportMode transport, int port)
        {
            SimpleTcpClient client;

            if (transport == BenchmarkTransportMode.Tcp)
            {
                client = new SimpleTcpClient(Host, port);
            }
            else
            {
                client = new SimpleTcpClient(Host, port, true, GetCertificatePath(), CertificatePassword);
                client.Settings.AcceptInvalidCertificates = true;
                client.Settings.MutuallyAuthenticate = false;
            }

            client.Settings.NoDelay = true;
            client.Settings.UseAsyncDataReceivedEvents = false;
            client.Settings.ReadTimeoutMs = 30000;
            return client;
        }

        private static string GetCertificatePath()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "simpletcp.pfx");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Unable to find test certificate at " + path + ".");
            }

            return path;
        }

        private static byte[] GetPayload(int sizeBytes)
        {
            lock (PayloadLock)
            {
                if (PayloadCache.TryGetValue(sizeBytes, out byte[] payload))
                {
                    return payload;
                }

                payload = new byte[sizeBytes];
                for (int i = 0; i < payload.Length; i++)
                {
                    payload[i] = (byte)(i % 251);
                }

                PayloadCache[sizeBytes] = payload;
                return payload;
            }
        }

        private static byte[] CreateAcknowledgementFrame(int payloadLength)
        {
            byte[] frame = new byte[FrameHeaderLength + sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, FrameHeaderLength), sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(FrameHeaderLength, sizeof(int)), payloadLength);
            return frame;
        }

        private static int GetAvailablePort()
        {
            for (int attempt = 0; attempt < MaxPortSelectionAttempts; attempt++)
            {
                int candidatePort = RandomNumberGenerator.GetInt32(MinBenchmarkPort, MaxBenchmarkPort);
                if (TryBindLoopbackPort(candidatePort))
                {
                    return candidatePort;
                }
            }

            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static bool TryBindLoopbackPort(int port)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);

            try
            {
                listener.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                }
            }
        }

        private static async Task WaitForConditionAsync(
            Func<bool> predicate,
            TimeSpan timeout,
            string failureMessage)
        {
            DateTime start = DateTime.UtcNow;

            while (DateTime.UtcNow - start < timeout)
            {
                if (predicate())
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            throw new TimeoutException(failureMessage);
        }

        private static async Task<T> WithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, string failureMessage)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != task)
            {
                throw new TimeoutException(failureMessage);
            }

            return await task.ConfigureAwait(false);
        }

        private static async Task WithTimeoutAsync(Task task, TimeSpan timeout, string failureMessage)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != task)
            {
                throw new TimeoutException(failureMessage);
            }

            await task.ConfigureAwait(false);
        }

        private static void PrintFinalSummary(
            DateTimeOffset runStarted,
            DateTimeOffset runCompleted,
            TimeSpan elapsed,
            IReadOnlyCollection<LatencyBenchmarkResult> latencyResults,
            IReadOnlyCollection<ThroughputBenchmarkResult> throughputResults,
            IReadOnlyCollection<ConnectionSetupBenchmarkResult> connectionSetupResults)
        {
            Console.WriteLine();
            Console.WriteLine("Benchmark Summary");
            Console.WriteLine("-----------------");
            Console.WriteLine("Run started  : " + runStarted.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            Console.WriteLine("Run completed: " + runCompleted.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            Console.WriteLine("Elapsed      : " + FormatDuration(elapsed.TotalSeconds));
            Console.WriteLine();

            PrintTable(
                "Response Time",
                new[] { "Transport", "Payload", "Iter", "Min ms", "Avg ms", "P50 ms", "P95 ms", "P99 ms", "Max ms" },
                latencyResults.Select(
                    result => new[]
                    {
                        result.Transport.ToString(),
                        result.PayloadName,
                        result.Iterations.ToString(),
                        FormatNumber(result.Summary.Min, 3),
                        FormatNumber(result.Summary.Average, 3),
                        FormatNumber(result.Summary.P50, 3),
                        FormatNumber(result.Summary.P95, 3),
                        FormatNumber(result.Summary.P99, 3),
                        FormatNumber(result.Summary.Max, 3)
                    }));

            PrintTable(
                "Throughput",
                new[] { "Transport", "Payload", "Clients", "Msg/Client", "Total Bytes", "Send ms", "End-to-End ms", "Msg/s", "MiB/s" },
                throughputResults.Select(
                    result => new[]
                    {
                        result.Transport.ToString(),
                        result.PayloadName,
                        result.ClientCount.ToString(),
                        result.MessagesPerClient.ToString(),
                        FormatBytes(result.TotalBytes),
                        FormatNumber(result.SendCompletionMs, 2),
                        FormatNumber(result.EndToEndCompletionMs, 2),
                        FormatNumber(result.MessagesPerSecond, 2),
                        FormatNumber(result.MebibytesPerSecond, 2)
                    }));

            PrintTable(
                "Connection Setup",
                new[] { "Transport", "Case", "Batch", "Batches", "Conn Avg ms", "Conn P95 ms", "Conn Max ms", "Batch Avg ms", "Batch P95 ms", "Batch Max ms" },
                connectionSetupResults.Select(
                    result => new[]
                    {
                        result.Transport.ToString(),
                        result.CaseName,
                        result.BatchSize.ToString(),
                        result.BatchIterations.ToString(),
                        FormatNumber(result.PerConnectionSummary.Average, 3),
                        FormatNumber(result.PerConnectionSummary.P95, 3),
                        FormatNumber(result.PerConnectionSummary.Max, 3),
                        FormatNumber(result.PerBatchSummary.Average, 3),
                        FormatNumber(result.PerBatchSummary.P95, 3),
                        FormatNumber(result.PerBatchSummary.Max, 3)
                    }));
        }

        private static void PrintTable(string title, string[] headers, IEnumerable<string[]> rows)
        {
            List<string[]> rowList = rows.ToList();
            int[] widths = new int[headers.Length];

            for (int i = 0; i < headers.Length; i++)
            {
                widths[i] = headers[i].Length;
            }

            foreach (string[] row in rowList)
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    widths[i] = Math.Max(widths[i], row[i] != null ? row[i].Length : 0);
                }
            }

            string border = "+" + String.Join("+", widths.Select(width => new string('-', width + 2))) + "+";

            Console.WriteLine(title);
            Console.WriteLine(border);
            Console.WriteLine("| " + String.Join(" | ", headers.Select((header, index) => PadRight(header, widths[index]))) + " |");
            Console.WriteLine(border);

            foreach (string[] row in rowList)
            {
                Console.WriteLine("| " + String.Join(" | ", row.Select((value, index) => PadRight(value, widths[index]))) + " |");
            }

            Console.WriteLine(border);
            Console.WriteLine();
        }

        private static string PadRight(string value, int width)
        {
            return (value ?? String.Empty).PadRight(width);
        }

        private static double StopwatchTicksToMilliseconds(long ticks)
        {
            return (ticks * 1000d) / Stopwatch.Frequency;
        }

        private static string FormatDuration(double totalSeconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(totalSeconds);
            if (duration.TotalMinutes >= 1)
            {
                return duration.TotalMinutes.ToString("N2") + " minutes";
            }

            return duration.TotalSeconds.ToString("N2") + " seconds";
        }

        private static string FormatBytes(long bytes)
        {
            double value = bytes;
            string[] units = { "B", "KB", "MB", "GB" };
            int unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString("N2") + " " + units[unit];
        }

        private static string FormatNumber(double value, int decimals)
        {
            return value.ToString("N" + decimals);
        }

        private static void SafeCleanupClient(SimpleTcpClient client)
        {
            if (client == null) return;

            try
            {
                if (client.IsConnected)
                {
                    client.Disconnect();
                }
            }
            catch (Exception e)
            {
                HandleCleanupException("client disconnect", e);
            }

            try
            {
                client.Dispose();
            }
            catch (Exception e)
            {
                HandleCleanupException("client dispose", e);
            }
        }

        private static void SafeCleanupServer(SimpleTcpServer server)
        {
            if (server == null) return;

            try
            {
                if (server.IsListening)
                {
                    server.Stop();
                }
            }
            catch (Exception e)
            {
                HandleCleanupException("server stop", e);
            }

            try
            {
                server.Dispose();
            }
            catch (Exception e)
            {
                HandleCleanupException("server dispose", e);
            }
        }

        private static void HandleCleanupException(string operation, Exception exception)
        {
            if (IsExpectedCleanupException(exception))
            {
                return;
            }

            Console.Error.WriteLine("Cleanup warning (" + operation + "): " + exception.Message);
        }

        private static bool IsExpectedCleanupException(Exception exception)
        {
            if (exception == null)
            {
                return true;
            }

            if (exception is AggregateException aggregateException)
            {
                return aggregateException
                    .Flatten()
                    .InnerExceptions
                    .All(IsExpectedCleanupException);
            }

            if (exception is TaskCanceledException) return true;
            if (exception is OperationCanceledException) return true;
            if (exception is ObjectDisposedException) return true;
            if (exception is IOException) return true;
            if (exception is InvalidOperationException) return true;
            if (exception is SocketException) return true;

            return false;
        }

        private static void ForceCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static void WriteDetail()
        {
            if (!_options.SummaryOnly)
            {
                Console.WriteLine();
            }
        }

        private static void WriteDetail(string value)
        {
            if (!_options.SummaryOnly)
            {
                Console.WriteLine(value);
            }
        }
    }

    internal enum BenchmarkTransportMode
    {
        Tcp,
        Ssl
    }

    internal sealed class PayloadBenchmarkCase
    {
        internal string Name { get; private set; }
        internal int SizeBytes { get; private set; }
        internal int LatencyIterations { get; private set; }
        internal int ThroughputMessagesPerClient { get; private set; }
        internal int[] ThroughputClientCounts { get; private set; }
        internal int ResponseTimeoutMs { get; private set; }
        internal int ThroughputTimeoutMs { get; private set; }

        internal PayloadBenchmarkCase(
            string name,
            int sizeBytes,
            int latencyIterations,
            int throughputMessagesPerClient,
            int[] throughputClientCounts,
            int responseTimeoutMs,
            int throughputTimeoutMs)
        {
            Name = name;
            SizeBytes = sizeBytes;
            LatencyIterations = latencyIterations;
            ThroughputMessagesPerClient = throughputMessagesPerClient;
            ThroughputClientCounts = throughputClientCounts;
            ResponseTimeoutMs = responseTimeoutMs;
            ThroughputTimeoutMs = throughputTimeoutMs;
        }
    }

    internal sealed class ConnectionSetupCase
    {
        internal string Name { get; private set; }
        internal int BatchSize { get; private set; }
        internal int BatchIterations { get; private set; }

        internal ConnectionSetupCase(string name, int batchSize, int batchIterations)
        {
            Name = name;
            BatchSize = batchSize;
            BatchIterations = batchIterations;
        }
    }

    internal sealed class ConnectionMeasurement
    {
        internal SimpleTcpClient Client { get; private set; }
        internal double ElapsedMilliseconds { get; private set; }

        internal ConnectionMeasurement(SimpleTcpClient client, double elapsedMilliseconds)
        {
            Client = client;
            ElapsedMilliseconds = elapsedMilliseconds;
        }
    }

    internal sealed class StatisticsSummary
    {
        internal double Min { get; private set; }
        internal double Average { get; private set; }
        internal double P50 { get; private set; }
        internal double P95 { get; private set; }
        internal double P99 { get; private set; }
        internal double Max { get; private set; }

        private StatisticsSummary()
        {
        }

        internal static StatisticsSummary From(IList<double> values)
        {
            if (values == null || values.Count < 1)
            {
                throw new ArgumentException("At least one value is required.", nameof(values));
            }

            List<double> ordered = values.OrderBy(v => v).ToList();

            StatisticsSummary summary = new StatisticsSummary();
            summary.Min = ordered[0];
            summary.Average = values.Average();
            summary.P50 = Percentile(ordered, 0.50);
            summary.P95 = Percentile(ordered, 0.95);
            summary.P99 = Percentile(ordered, 0.99);
            summary.Max = ordered[ordered.Count - 1];
            return summary;
        }

        private static double Percentile(IList<double> orderedValues, double percentile)
        {
            if (orderedValues.Count == 1)
            {
                return orderedValues[0];
            }

            double position = percentile * (orderedValues.Count - 1);
            int lowerIndex = (int)Math.Floor(position);
            int upperIndex = (int)Math.Ceiling(position);

            if (lowerIndex == upperIndex)
            {
                return orderedValues[lowerIndex];
            }

            double weight = position - lowerIndex;
            return orderedValues[lowerIndex] + ((orderedValues[upperIndex] - orderedValues[lowerIndex]) * weight);
        }
    }

    internal sealed class LatencyBenchmarkResult
    {
        internal BenchmarkTransportMode Transport { get; private set; }
        internal string PayloadName { get; private set; }
        internal int Iterations { get; private set; }
        internal StatisticsSummary Summary { get; private set; }

        internal LatencyBenchmarkResult(
            BenchmarkTransportMode transport,
            string payloadName,
            int iterations,
            StatisticsSummary summary)
        {
            Transport = transport;
            PayloadName = payloadName;
            Iterations = iterations;
            Summary = summary;
        }
    }

    internal sealed class ThroughputBenchmarkResult
    {
        internal BenchmarkTransportMode Transport { get; private set; }
        internal string PayloadName { get; private set; }
        internal int ClientCount { get; private set; }
        internal int MessagesPerClient { get; private set; }
        internal long TotalBytes { get; private set; }
        internal double SendCompletionMs { get; private set; }
        internal double EndToEndCompletionMs { get; private set; }
        internal double MessagesPerSecond { get; private set; }
        internal double MebibytesPerSecond { get; private set; }

        internal ThroughputBenchmarkResult(
            BenchmarkTransportMode transport,
            string payloadName,
            int clientCount,
            int messagesPerClient,
            long totalBytes,
            double sendCompletionMs,
            double endToEndCompletionMs,
            double messagesPerSecond,
            double mebibytesPerSecond)
        {
            Transport = transport;
            PayloadName = payloadName;
            ClientCount = clientCount;
            MessagesPerClient = messagesPerClient;
            TotalBytes = totalBytes;
            SendCompletionMs = sendCompletionMs;
            EndToEndCompletionMs = endToEndCompletionMs;
            MessagesPerSecond = messagesPerSecond;
            MebibytesPerSecond = mebibytesPerSecond;
        }
    }

    internal sealed class ConnectionSetupBenchmarkResult
    {
        internal BenchmarkTransportMode Transport { get; private set; }
        internal string CaseName { get; private set; }
        internal int BatchSize { get; private set; }
        internal int BatchIterations { get; private set; }
        internal StatisticsSummary PerConnectionSummary { get; private set; }
        internal StatisticsSummary PerBatchSummary { get; private set; }

        internal ConnectionSetupBenchmarkResult(
            BenchmarkTransportMode transport,
            string caseName,
            int batchSize,
            int batchIterations,
            StatisticsSummary perConnectionSummary,
            StatisticsSummary perBatchSummary)
        {
            Transport = transport;
            CaseName = caseName;
            BatchSize = batchSize;
            BatchIterations = batchIterations;
            PerConnectionSummary = perConnectionSummary;
            PerBatchSummary = perBatchSummary;
        }
    }

    internal sealed class BenchmarkCommandLineOptions
    {
        internal bool SummaryOnly { get; private set; }
        internal bool Quick { get; private set; }

        internal static BenchmarkCommandLineOptions Parse(string[] args)
        {
            BenchmarkCommandLineOptions options = new BenchmarkCommandLineOptions();

            if (args == null)
            {
                return options;
            }

            foreach (string arg in args)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(arg, "--summary-only"))
                {
                    options.SummaryOnly = true;
                }
                else if (StringComparer.OrdinalIgnoreCase.Equals(arg, "--quick"))
                {
                    options.Quick = true;
                }
            }

            return options;
        }
    }

    internal sealed class FrameAccumulator
    {
        private readonly object _sync = new object();
        private byte[] _buffer = new byte[8192];
        private int _count;

        internal void Append(ArraySegment<byte> data, Action<byte[], int, int> onFrame)
        {
            if (data.Array == null || data.Count < 1) return;
            if (onFrame == null) throw new ArgumentNullException(nameof(onFrame));

            lock (_sync)
            {
                EnsureCapacity(_count + data.Count);
                Buffer.BlockCopy(data.Array, data.Offset, _buffer, _count, data.Count);
                _count += data.Count;

                while (true)
                {
                    if (_count < ProgramFrameConstants.HeaderLength)
                    {
                        return;
                    }

                    int frameLength = BinaryPrimitives.ReadInt32LittleEndian(
                        new ReadOnlySpan<byte>(_buffer, 0, ProgramFrameConstants.HeaderLength));

                    if (frameLength < 0)
                    {
                        throw new InvalidOperationException("Frame length cannot be negative.");
                    }

                    int totalFrameLength = ProgramFrameConstants.HeaderLength + frameLength;
                    if (_count < totalFrameLength)
                    {
                        return;
                    }

                    onFrame(_buffer, ProgramFrameConstants.HeaderLength, frameLength);

                    int bytesRemaining = _count - totalFrameLength;
                    if (bytesRemaining > 0)
                    {
                        Buffer.BlockCopy(_buffer, totalFrameLength, _buffer, 0, bytesRemaining);
                    }

                    _count = bytesRemaining;
                }
            }
        }

        private void EnsureCapacity(int requiredLength)
        {
            if (requiredLength <= _buffer.Length) return;

            int newLength = _buffer.Length;
            while (newLength < requiredLength)
            {
                newLength = checked(newLength * 2);
            }

            Array.Resize(ref _buffer, newLength);
        }
    }

    internal sealed class AcknowledgementMailbox
    {
        private readonly object _sync = new object();
        private readonly FrameAccumulator _frames = new FrameAccumulator();
        private TaskCompletionSource<int> _pending;

        internal Task<int> ExpectAcknowledgement()
        {
            lock (_sync)
            {
                if (_pending != null)
                {
                    throw new InvalidOperationException("A latency probe is already awaiting acknowledgement.");
                }

                _pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _pending.Task;
            }
        }

        internal void Append(ArraySegment<byte> data)
        {
            _frames.Append(
                data,
                (buffer, offset, count) =>
                {
                    if (count != sizeof(int))
                    {
                        throw new InvalidOperationException(
                            "Acknowledgement payload must be " + sizeof(int) + " bytes, received " + count + ".");
                    }

                    int value = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(buffer, offset, count));
                    TaskCompletionSource<int> pending;

                    lock (_sync)
                    {
                        pending = _pending;
                        _pending = null;
                    }

                    if (pending == null)
                    {
                        throw new InvalidOperationException("Received an acknowledgement without a pending latency probe.");
                    }

                    pending.TrySetResult(value);
                });
        }

        internal void Fail(Exception exception)
        {
            TaskCompletionSource<int> pending;

            lock (_sync)
            {
                pending = _pending;
                _pending = null;
            }

            pending?.TrySetException(exception);
        }
    }

    internal sealed class FramedPayloadStream : Stream
    {
        private readonly byte[] _header = new byte[ProgramFrameConstants.HeaderLength];
        private readonly byte[] _payload;
        private long _position;

        internal FramedPayloadStream(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            _payload = payload;
            BinaryPrimitives.WriteInt32LittleEndian(_header.AsSpan(), payload.Length);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => ProgramFrameConstants.HeaderLength + _payload.LongLength;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (_position >= Length || buffer.Length == 0)
            {
                return 0;
            }

            int totalRead = 0;

            if (_position < ProgramFrameConstants.HeaderLength)
            {
                int headerOffset = (int)_position;
                int headerBytesToRead = Math.Min(buffer.Length, ProgramFrameConstants.HeaderLength - headerOffset);
                _header.AsSpan(headerOffset, headerBytesToRead).CopyTo(buffer);
                _position += headerBytesToRead;
                totalRead += headerBytesToRead;
                buffer = buffer.Slice(headerBytesToRead);
            }

            if (buffer.Length > 0 && _position < Length)
            {
                int payloadOffset = (int)(_position - ProgramFrameConstants.HeaderLength);
                int payloadBytesToRead = Math.Min(buffer.Length, _payload.Length - payloadOffset);
                _payload.AsSpan(payloadOffset, payloadBytesToRead).CopyTo(buffer);
                _position += payloadBytesToRead;
                totalRead += payloadBytesToRead;
            }

            return totalRead;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<int>(Read(buffer.Span));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    internal static class ProgramFrameConstants
    {
        internal const int HeaderLength = 4;
    }
}
