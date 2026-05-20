namespace Test.Shared;

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using SuperSimpleTcp;

internal static class TestEnvironment
{
    public const string LoopbackIp = "127.0.0.1";
    private const string CertificatePassword = "simpletcp";

    public static string CertificatePath =>
        Path.Combine(AppContext.BaseDirectory, "simpletcp.pfx");

    public static byte[] GetCertificateBytes()
    {
        return File.ReadAllBytes(CertificatePath);
    }

    public static byte[] ExportCertificateBytes()
    {
        using X509Certificate2 cert = LoadCertificate(CertificatePassword);
        return cert.Export(X509ContentType.Pfx);
    }

    public static X509Certificate2 LoadCertificate(string? password = null)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12FromFile(
            CertificatePath,
            password,
            X509KeyStorageFlags.Exportable);
#else
        return new X509Certificate2(
            CertificatePath,
            password,
            X509KeyStorageFlags.Exportable);
#endif
    }

    public static X509Certificate2 LoadCertificate(byte[] certificateBytes, string? password = null)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(
            certificateBytes,
            password,
            X509KeyStorageFlags.Exportable);
#else
        return new X509Certificate2(
            certificateBytes,
            password,
            X509KeyStorageFlags.Exportable);
#endif
    }

    public static async Task DelayForIoAsync(CancellationToken token = default)
    {
        await Task.Delay(100, token).ConfigureAwait(false);
    }

    public static async Task WaitForConditionAsync(
        Func<bool> predicate,
        TimeSpan timeout,
        string failureMessage,
        CancellationToken token = default)
    {
        DateTime start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            token.ThrowIfCancellationRequested();

            if (predicate())
                return;

            await Task.Delay(20, token).ConfigureAwait(false);
        }

        throw new TimeoutException(failureMessage);
    }

    public static async Task<T> WithTimeoutAsync<T>(
        Task<T> task,
        TimeSpan timeout,
        string failureMessage)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (completed != task)
            throw new TimeoutException(failureMessage);

        return await task.ConfigureAwait(false);
    }

    public static async Task WithTimeoutAsync(
        Task task,
        TimeSpan timeout,
        string failureMessage)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        if (completed != task)
            throw new TimeoutException(failureMessage);

        await task.ConfigureAwait(false);
    }

    public static string GetString(ArraySegment<byte> data)
    {
        return Encoding.UTF8.GetString(data.Array!, data.Offset, data.Count);
    }

    public static MemoryStream StreamFromString(string data)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(data));
    }

    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(
            Enumerable.Range(0, length)
                .Select(_ => chars[Random.Shared.Next(chars.Length)])
                .ToArray());
    }

    public static int GetFreeTcpPort()
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    public static string GetSingleClient(SimpleTcpServer server)
    {
        List<string> clients = server.GetClients().ToList();
        TestAssert.Equal(1, clients.Count, "Expected exactly one connected client.");
        return clients[0];
    }

    public static UnreadableStream CreateUnreadableStream()
    {
        return new UnreadableStream();
    }

    public static async Task<BacklogBlackhole> RequireBacklogBlackholeAsync()
    {
        BacklogBlackhole? blackhole = await BacklogBlackhole.TryCreateAsync(
            backlog: 1,
            probeHangThreshold: TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);

        if (blackhole == null)
        {
            throw new InvalidOperationException(
                "Unable to create a deterministic hanging connect endpoint on this machine.");
        }

        return blackhole;
    }

    internal sealed class UnreadableStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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

    internal sealed class BacklogBlackhole : IDisposable
    {
        private readonly Socket _listener;
        private readonly List<Socket> _fillers;

        private BacklogBlackhole(Socket listener, List<Socket> fillers, int port)
        {
            _listener = listener;
            _fillers = fillers;
            Port = port;
        }

        public int Port { get; }

        public static async Task<BacklogBlackhole?> TryCreateAsync(
            int backlog,
            TimeSpan probeHangThreshold)
        {
            Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            List<Socket> fillers = new(capacity: Math.Max(1, backlog));

            try
            {
                listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                int port = ((IPEndPoint)listener.LocalEndPoint!).Port;
                listener.Listen(backlog);

                for (int i = 0; i < backlog; i++)
                {
                    Socket filler = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    filler.Connect(IPAddress.Loopback, port);
                    fillers.Add(filler);
                }

                bool hangs = await ProbeConnectHangsAsync(
                    IPAddress.Loopback,
                    port,
                    probeHangThreshold).ConfigureAwait(false);

                if (!hangs)
                {
                    foreach (Socket filler in fillers)
                        SafeDispose(filler);

                    SafeDispose(listener);
                    return null;
                }

                return new BacklogBlackhole(listener, fillers, port);
            }
            catch
            {
                foreach (Socket filler in fillers)
                    SafeDispose(filler);

                SafeDispose(listener);
                throw;
            }
        }

        public async Task AcceptOneAsync()
        {
            try
            {
                using CancellationTokenSource cts = new(100);
                Socket accepted = await _listener.AcceptAsync(cts.Token).ConfigureAwait(false);
                accepted.Dispose();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            foreach (Socket filler in _fillers)
                SafeDispose(filler);

            SafeDispose(_listener);
        }

        private static async Task<bool> ProbeConnectHangsAsync(
            IPAddress ipAddress,
            int port,
            TimeSpan threshold)
        {
            using Socket probe = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using CancellationTokenSource cts = new(threshold);

            try
            {
                await probe.ConnectAsync(ipAddress, port, cts.Token).ConfigureAwait(false);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
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

        private static void SafeDispose(Socket socket)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }

            try
            {
                socket.Close();
            }
            catch
            {
            }

            try
            {
                socket.Dispose();
            }
            catch
            {
            }
        }
    }
}
