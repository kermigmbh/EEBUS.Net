using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TestProject1.IntegrationTests
{
    /// <summary>
    /// Subscribes to an event up front and buffers every raised value, letting you supply the
    /// match predicate later at await time via <see cref="Match"/>. Construct the waiter (which
    /// subscribes immediately), run your trigger/init logic, then
    /// <c>await waiter.Match(value =&gt; ...)</c>. Because every event from construction is buffered,
    /// a matching event raised while the trigger logic runs cannot be missed; the buffered values
    /// are replayed against the late predicate. Dispose (e.g. via <c>using</c>) to unsubscribe.
    /// </summary>
    public sealed class TestWaiter<T> : IDisposable
    {
        private readonly object _gate = new();
        private readonly List<T> _buffer = new();
        private readonly Action _unsubscribe;

        private Func<T, bool>? _pendingMatch;
        private TaskCompletionSource<T>? _pendingCompletion;
        private int _disposed;

        /// <summary>
        /// Subscribes immediately to an <see cref="EventHandler{T}"/>-shaped event.
        /// <paramref name="subscribe"/> wires the buffering handler to the event (e.g.
        /// <c>handler =&gt; source.OnEvent += handler</c>); supply <paramref name="unsubscribe"/>
        /// for symmetric removal on <see cref="Dispose"/>.
        /// </summary>
        public TestWaiter(
            Action<EventHandler<T>> subscribe,
            Action<EventHandler<T>>? unsubscribe = null)
        {
            ArgumentNullException.ThrowIfNull(subscribe);

            void Handler(object? sender, T value) => Receive(value);

            subscribe(Handler);
            _unsubscribe = unsubscribe == null ? () => { } : () => unsubscribe(Handler);
        }

        /// <summary>
        /// Subscribes immediately to a <see cref="Func{T, TResult}"/>-shaped (async) event.
        /// <paramref name="subscribe"/> wires the buffering handler to the event (e.g.
        /// <c>handler =&gt; source.OnEvent += handler</c>); supply <paramref name="unsubscribe"/>
        /// for symmetric removal on <see cref="Dispose"/>.
        /// </summary>
        public TestWaiter(
            Action<Func<T, Task>> subscribe,
            Action<Func<T, Task>>? unsubscribe = null)
        {
            ArgumentNullException.ThrowIfNull(subscribe);

            Task Handler(T value)
            {
                Receive(value);
                return Task.CompletedTask;
            }

            subscribe(Handler);
            _unsubscribe = unsubscribe == null ? () => { } : () => unsubscribe(Handler);
        }

        private void Receive(T value)
        {
            TaskCompletionSource<T>? toComplete = null;

            lock (_gate)
            {
                if (_pendingMatch != null && _pendingMatch(value))
                {
                    toComplete = _pendingCompletion;
                    _pendingMatch = null;
                    _pendingCompletion = null;
                }
                else
                {
                    _buffer.Add(value);
                }
            }

            toComplete?.TrySetResult(value);
        }

        /// <summary>
        /// Awaits the first event (already buffered or arriving later) that satisfies
        /// <paramref name="predicate"/>, throwing <see cref="TimeoutException"/> if none arrives
        /// within <paramref name="timeoutMs"/>. The matched value is consumed from the buffer.
        /// </summary>
        public async Task<T> Match(Func<T, bool> predicate, int timeoutMs = 5000)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_gate)
            {
                if (_pendingMatch != null)
                    throw new InvalidOperationException("A Match is already in progress on this waiter.");

                for (int i = 0; i < _buffer.Count; i++)
                {
                    if (predicate(_buffer[i]))
                    {
                        var value = _buffer[i];
                        _buffer.RemoveAt(i);
                        return value;
                    }
                }

                _pendingMatch = predicate;
                _pendingCompletion = tcs;
            }

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);

            if (completed != tcs.Task)
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_pendingCompletion, tcs))
                    {
                        _pendingMatch = null;
                        _pendingCompletion = null;
                    }
                }

                throw new TimeoutException("No matching event was raised within timeout");
            }

            return await tcs.Task.ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _unsubscribe();
        }
    }

    /// <summary>
    /// Two-argument counterpart of <see cref="TestWaiter{T}"/> for
    /// <see cref="Func{T1, T2, TResult}"/>-shaped (async) events. Buffers every raised pair and
    /// matches with <c>await waiter.Match((a, b) =&gt; ...)</c>.
    /// </summary>
    public sealed class TestWaiter<T1, T2> : IDisposable
    {
        private readonly object _gate = new();
        private readonly List<(T1, T2)> _buffer = new();
        private readonly Action _unsubscribe;

        private Func<T1, T2, bool>? _pendingMatch;
        private TaskCompletionSource<(T1, T2)>? _pendingCompletion;
        private int _disposed;

        /// <summary>
        /// Subscribes immediately to a two-argument <see cref="Func{T1, T2, TResult}"/>-shaped
        /// (async) event. <paramref name="subscribe"/> wires the buffering handler to the event;
        /// supply <paramref name="unsubscribe"/> for symmetric removal on <see cref="Dispose"/>.
        /// </summary>
        public TestWaiter(
            Action<Func<T1, T2, Task>> subscribe,
            Action<Func<T1, T2, Task>>? unsubscribe = null)
        {
            ArgumentNullException.ThrowIfNull(subscribe);

            Task Handler(T1 arg1, T2 arg2)
            {
                Receive(arg1, arg2);
                return Task.CompletedTask;
            }

            subscribe(Handler);
            _unsubscribe = unsubscribe == null ? () => { } : () => unsubscribe(Handler);
        }

        private void Receive(T1 arg1, T2 arg2)
        {
            TaskCompletionSource<(T1, T2)>? toComplete = null;

            lock (_gate)
            {
                if (_pendingMatch != null && _pendingMatch(arg1, arg2))
                {
                    toComplete = _pendingCompletion;
                    _pendingMatch = null;
                    _pendingCompletion = null;
                }
                else
                {
                    _buffer.Add((arg1, arg2));
                }
            }

            toComplete?.TrySetResult((arg1, arg2));
        }

        /// <summary>
        /// Awaits the first event pair (already buffered or arriving later) that satisfies
        /// <paramref name="predicate"/>, throwing <see cref="TimeoutException"/> if none arrives
        /// within <paramref name="timeoutMs"/>. The matched pair is consumed from the buffer.
        /// </summary>
        public async Task<(T1, T2)> Match(Func<T1, T2, bool> predicate, int timeoutMs = 5000)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            var tcs = new TaskCompletionSource<(T1, T2)>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_gate)
            {
                if (_pendingMatch != null)
                    throw new InvalidOperationException("A Match is already in progress on this waiter.");

                for (int i = 0; i < _buffer.Count; i++)
                {
                    var (a, b) = _buffer[i];
                    if (predicate(a, b))
                    {
                        _buffer.RemoveAt(i);
                        return (a, b);
                    }
                }

                _pendingMatch = predicate;
                _pendingCompletion = tcs;
            }

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);

            if (completed != tcs.Task)
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_pendingCompletion, tcs))
                    {
                        _pendingMatch = null;
                        _pendingCompletion = null;
                    }
                }

                throw new TimeoutException("No matching event was raised within timeout");
            }

            return await tcs.Task.ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _unsubscribe();
        }
    }
}
