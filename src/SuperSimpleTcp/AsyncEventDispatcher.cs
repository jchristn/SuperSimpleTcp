namespace SuperSimpleTcp
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class AsyncEventDispatcher<T> : IDisposable
    {
        private readonly Action<T> _handler;
        private readonly Action<Exception> _onHandlerException;
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly List<Task> _workers;
        private bool _disposed;

        internal AsyncEventDispatcher(Action<T> handler, int workerCount, Action<Exception> onHandlerException = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (workerCount < 1) throw new ArgumentOutOfRangeException(nameof(workerCount));

            _handler = handler;
            _onHandlerException = onHandlerException;
            _workers = new List<Task>(workerCount);

            for (int i = 0; i < workerCount; i++)
            {
                _workers.Add(Task.Run(() => WorkerLoopAsync(_tokenSource.Token)));
            }
        }

        internal void Enqueue(T item)
        {
            if (_disposed) return;

            _queue.Enqueue(item);
            try
            {
                _signal.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        internal int DrainPending()
        {
            int count = 0;
            while (_queue.TryDequeue(out T _))
            {
                count++;
            }

            return count;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _tokenSource.Cancel();
            }
            catch
            {
            }

            try
            {
                for (int i = 0; i < _workers.Count; i++)
                {
                    _signal.Release();
                }
            }
            catch
            {
            }

            try
            {
                Task.WaitAll(_workers.ToArray(), 2000);
            }
            catch
            {
            }

            _signal.Dispose();
            _tokenSource.Dispose();
        }

        private async Task WorkerLoopAsync(CancellationToken token)
        {
            // The worker is started from whatever flow called Start() or Connect(); do not let handler
            // spans inherit that ambient span.  Each item carries its own parent context instead.
            Activity.Current = null;

            while (true)
            {
                try
                {
                    await _signal.WaitAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (_disposed && _queue.IsEmpty) break;

                while (_queue.TryDequeue(out T item))
                {
                    // A throwing handler must not end the worker; otherwise every later item queues forever.
                    try
                    {
                        _handler(item);
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            _onHandlerException?.Invoke(e);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }
    }
}
