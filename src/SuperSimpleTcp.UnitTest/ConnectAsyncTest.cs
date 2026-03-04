using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SuperSimpleTcp.UnitTest;

[TestClass]
public sealed class ConnectAsyncTest
{
    private const string LoopbackIp = "127.0.0.1";

    [TestMethod]
    public async Task ConnectAsync_ConcurrentCalls_OnlyOnePhysicalConnection()
    {
        var serverConnectedCount = 0;
        void ServerClientConnected(object? sender, ConnectionEventArgs e) =>
            Interlocked.Increment(ref serverConnectedCount);

        using var server = new SimpleTcpServer(LoopbackIp, 0);
        server.Events.ClientConnected += ServerClientConnected;
        server.Start();
        var port = server.Port;

        using var client = new SimpleTcpClient($"{LoopbackIp}:{port}");

        var tasks = Enumerable
            .Range(0, 25)
            .Select(_ => Task.Run(() => client.ConnectAsync()))
            .ToArray();

        await WithTimeoutAsync(Task.WhenAll(tasks), TimeSpan.FromSeconds(5));

        await Task.Delay(50);

        Assert.AreEqual(1, serverConnectedCount);
        Assert.IsTrue(client.IsConnected);

        client.Disconnect();
        server.Stop();
        server.Events.ClientConnected -= ServerClientConnected;
    }

    [TestMethod]
    public async Task ConnectAsync_TokenAlreadyCanceled_ThrowsOperationCanceledException_AndDoesNotConnect()
    {
        var serverConnectedCount = 0;
        void ServerClientConnected(object? sender, ConnectionEventArgs e) =>
            Interlocked.Increment(ref serverConnectedCount);

        using var server = new SimpleTcpServer(LoopbackIp, 0);
        server.Events.ClientConnected += ServerClientConnected;
        server.Start();
        var port = server.Port;

        using var client = new SimpleTcpClient($"{LoopbackIp}:{port}");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            WithTimeoutAsync(client.ConnectAsync(cts.Token), TimeSpan.FromSeconds(2))
        );

        await Task.Delay(50);

        Assert.AreEqual(0, serverConnectedCount);
        Assert.IsFalse(client.IsConnected);

        server.Stop();
        server.Events.ClientConnected -= ServerClientConnected;
    }

    [TestMethod]
    public async Task ConnectAsync_WhenConnectFaults_PropagatesSocketException()
    {
        // Get an OS-assigned port that has no listener. Bind (without listen) avoids TIME_WAIT.
        int port;
        using (var temp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
            temp.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            port = ((IPEndPoint)temp.LocalEndPoint!).Port;
        }

        // Nothing listens on this port.
        using var client = new SimpleTcpClient($"{LoopbackIp}:{port}");

        // Keep timeout large; we want the immediate connect failure path (connectTask completes first).
        client.Settings.ConnectTimeoutMs = 5000;

        await Assert.ThrowsExactlyAsync<SocketException>(() => client.ConnectAsync());

        Assert.IsFalse(client.IsConnected);
    }

    [TestMethod]
    public async Task ConnectAsync_Timeout_ThrowsTimeoutException_DoesNotDeadlockMutex_OnSecondAttempt()
    {
        using var blackhole = await RequireBacklogBlackholeAsync();

        using var client = new SimpleTcpClient($"{LoopbackIp}:{blackhole.Port}");
        client.Settings.ConnectTimeoutMs = 150;

        await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            WithTimeoutAsync(client.ConnectAsync(), TimeSpan.FromSeconds(2))
        );

        Assert.IsFalse(client.IsConnected);

        // Second attempt should also finish promptly (verifies mutex is released on exception paths).
        await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            WithTimeoutAsync(client.ConnectAsync(), TimeSpan.FromSeconds(2))
        );

        Assert.IsFalse(client.IsConnected);
    }

    [TestMethod]
    public async Task ConnectAsync_SuccessfulConnect_OnSecondAttempt()
    {
        using var blackhole = await RequireBacklogBlackholeAsync();

        using var client = new SimpleTcpClient($"{LoopbackIp}:{blackhole.Port}");
        client.Settings.ConnectTimeoutMs = 150;

        await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            WithTimeoutAsync(client.ConnectAsync(), TimeSpan.FromSeconds(2))
        );

        Assert.IsFalse(client.IsConnected);

        await blackhole.AcceptOne();

        // Second attempt should also finish promptly (verifies mutex is released on exception paths).
        await WithTimeoutAsync(client.ConnectAsync(), TimeSpan.FromSeconds(2));

        Assert.IsTrue(client.IsConnected);
    }

    [TestMethod]
    public async Task ConnectAsync_CancellationWhileWaitingForMutex_ThrowsOperationCanceledException()
    {
        using var blackhole = await RequireBacklogBlackholeAsync();

        using var client = new SimpleTcpClient($"{LoopbackIp}:{blackhole.Port}");
        client.Settings.ConnectTimeoutMs = 5000;

        // Task A holds the mutex while "connecting" until it is canceled.
        using var ctsA = new CancellationTokenSource();
        ctsA.CancelAfter(2000);
        var taskA = Task.Run(() => client.ConnectAsync(ctsA.Token));

        // Give A a moment to enter ConnectAsync and acquire the mutex.
        await Task.Delay(25);

        // Task B should block on mutex, then get canceled while waiting.
        using var ctsB = new CancellationTokenSource();
        ctsB.CancelAfter(50);

        var taskB = Task.Run(() => client.ConnectAsync(ctsB.Token));

        // B must complete quickly with OCE (wait cancellation path).
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            WithTimeoutAsync(taskB, TimeSpan.FromSeconds(2))
        );

        await blackhole.AcceptOne();

        await taskA;

        Assert.IsTrue(client.IsConnected);
    }

    [TestMethod]
    public async Task ConnectAsync_CancelBeatsTimeout_PrioritizesCancellation()
    {
        using var blackhole = await RequireBacklogBlackholeAsync();

        using var client = new SimpleTcpClient($"{LoopbackIp}:{blackhole.Port}");
        client.Settings.ConnectTimeoutMs = 200;

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(25);

        // When timeout task is canceled, ConnectCoreAsync must throw cancellation (not TimeoutException).
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            WithTimeoutAsync(client.ConnectAsync(cts.Token), TimeSpan.FromSeconds(2))
        );

        Assert.IsFalse(client.IsConnected);
    }

    [TestMethod]
    public async Task ConnectAsync_TimeoutAndCancellation_DoNotProduceUnobservedTaskException_FromPendingConnectTask()
    {
        using var blackhole = await RequireBacklogBlackholeAsync();

        // Flush any pre-existing unobserved exceptions from prior tests so they
        // don't get attributed to this test when we call GC.Collect below.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var unobserved = new ConcurrentQueue<AggregateException>();
        void Handler(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            unobserved.Enqueue(e.Exception);
            e.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += Handler;
        try
        {
            using var client = new SimpleTcpClient($"{LoopbackIp}:{blackhole.Port}");
            client.Settings.ConnectTimeoutMs = 150;

            await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
                WithTimeoutAsync(client.ConnectAsync(), TimeSpan.FromSeconds(2))
            );

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(75);
            client.Settings.ConnectTimeoutMs = 5000;

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                WithTimeoutAsync(client.ConnectAsync(cts.Token), TimeSpan.FromSeconds(2))
            );

            // Allow brief async settlement for ContinueWith observation to complete.
            await Task.Delay(50);

            // Encourage finalizers to run and surface any unobserved exceptions.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.AreEqual(0, unobserved.Count);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Handler;
        }
    }

    [TestMethod]
    public async Task ConnectAsync_CancelAfterSuccessfulConnect_DoesNotDisconnect()
    {
        using var server = new SimpleTcpServer(LoopbackIp, 0);
        server.Start();
        var port = server.Port;

        using var client = new SimpleTcpClient($"{LoopbackIp}:{port}");

        using var cts = new CancellationTokenSource();

        await WithTimeoutAsync(client.ConnectAsync(cts.Token), TimeSpan.FromSeconds(2));
        Assert.IsTrue(client.IsConnected);

        cts.Cancel();

        // External connect token cancellation should not affect an already-established connection.
        await Task.Delay(25);
        Assert.IsTrue(client.IsConnected);

        client.Disconnect();
        server.Stop();
    }

    // -------- helpers --------

    private static async Task WithTimeoutAsync(Task task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (completed != task)
            Assert.Fail($"Operation did not complete within {timeout.TotalMilliseconds}ms.");

        await task.ConfigureAwait(false);
    }

    private static async Task<BacklogBlackhole> RequireBacklogBlackholeAsync()
    {
        // Creates a local endpoint that (on most OS/network stacks) causes an additional connect to hang
        // by saturating the listen/accept queue. If the environment doesn't exhibit the hang behavior,
        // timeout/cancel race tests cannot be made deterministic and are marked inconclusive.
        var blackhole = await BacklogBlackhole
            .TryCreateAsync(backlog: 1, probeHangThreshold: TimeSpan.FromMilliseconds(200))
            .ConfigureAwait(false);

        if (blackhole is null)
        {
            Assert.Inconclusive(
                "Could not create a deterministic 'hanging connect' endpoint on this environment. "
                    + "OS/network stack did not block additional connects when the accept queue was saturated."
            );
            throw new InvalidOperationException("Unreachable.");
        }

        return blackhole;
    }

    private sealed class BacklogBlackhole : IDisposable
    {
        private readonly Socket _listener;
        private readonly List<Socket> _fillers;

        public int Port { get; }

        private BacklogBlackhole(Socket listener, List<Socket> fillers, int port)
        {
            _listener = listener;
            _fillers = fillers;
            Port = port;
        }

        public static async Task<BacklogBlackhole?> TryCreateAsync(
            int backlog,
            TimeSpan probeHangThreshold
        )
        {
            var listener = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            var fillers = new List<Socket>(capacity: Math.Max(1, backlog));

            try
            {
                listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                var port = ((IPEndPoint)listener.LocalEndPoint!).Port;

                listener.Listen(backlog);

                // Fill exactly 'backlog' slots (these connects should succeed quickly).
                for (var i = 0; i < backlog; i++)
                {
                    var s = new Socket(
                        AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp
                    );
                    s.Connect(IPAddress.Loopback, port);
                    fillers.Add(s);
                }

                // Probe whether an extra connect hangs beyond the threshold.
                var hangs = await ProbeConnectHangsAsync(
                        IPAddress.Loopback,
                        port,
                        probeHangThreshold
                    )
                    .ConfigureAwait(false);
                if (!hangs)
                {
                    foreach (var s in fillers)
                        SafeDispose(s);
                    SafeDispose(listener);
                    return null;
                }

                return new BacklogBlackhole(listener, fillers, port);
            }
            catch
            {
                foreach (var s in fillers)
                    SafeDispose(s);
                SafeDispose(listener);
                throw;
            }
        }

        private static async Task<bool> ProbeConnectHangsAsync(IPAddress ip, int port, TimeSpan threshold)
        {
            using var probe = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );
            using var cts = new CancellationTokenSource(threshold);

            try
            {
                await probe.ConnectAsync(ip, port, cts.Token).ConfigureAwait(false);
                return false; // Connected quickly => cannot force timeout deterministically.
            }
            catch (OperationCanceledException)
            {
                return true; // Hung beyond threshold => suitable for timeout/cancel race tests.
            }
            catch (SocketException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task AcceptOne()
        {
            try
            {
                var s = await _listener.AcceptAsync(new CancellationTokenSource(10).Token);
                s.Dispose();
            }
            catch { }
        }

        public void Dispose()
        {
            foreach (var s in _fillers)
                SafeDispose(s);
            SafeDispose(_listener);
        }

        private static void SafeDispose(Socket s)
        {
            try
            {
                s.Shutdown(SocketShutdown.Both);
            }
            catch { }
            try
            {
                s.Close();
            }
            catch { }
            try
            {
                s.Dispose();
            }
            catch { }
        }
    }
}
