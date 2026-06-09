using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FrameWork.Tools
{
    public sealed class HighPrecisionTimer : IDisposable
    {
        private readonly TimeSpan _interval;
        private readonly Action<HighPrecisionTimerTick> _tickAction;
        private readonly Action<Exception>? _errorAction;
        private readonly object _stateLock = new();
        private CancellationTokenSource? _cts;
        private Task? _timerTask;
        private bool _disposed;

        public HighPrecisionTimer(
            TimeSpan interval,
            Action<HighPrecisionTimerTick> tickAction,
            Action<Exception>? errorAction = null)
        {
            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than zero.");
            }

            _interval = interval;
            _tickAction = tickAction ?? throw new ArgumentNullException(nameof(tickAction));
            _errorAction = errorAction;
        }

        public bool IsRunning
        {
            get
            {
                lock (_stateLock)
                {
                    return _timerTask != null && !_timerTask.IsCompleted;
                }
            }
        }

        public void Start()
        {
            lock (_stateLock)
            {
                ThrowIfDisposed();
                if (_timerTask != null && !_timerTask.IsCompleted)
                {
                    return;
                }

                _cts = new CancellationTokenSource();
                _timerTask = Task.Run(() => RunAsync(_cts.Token));
            }
        }

        public void Stop()
        {
            CancellationTokenSource? cts;
            lock (_stateLock)
            {
                cts = _cts;
                _cts = null;
                _timerTask = null;
            }

            if (cts == null)
            {
                return;
            }

            cts.Cancel();
            cts.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            long intervalTicks = (long)(_interval.TotalSeconds * Stopwatch.Frequency);
            if (intervalTicks <= 0)
            {
                intervalTicks = 1;
            }

            long startTimestamp = Stopwatch.GetTimestamp();
            long nextTimestamp = startTimestamp + intervalTicks;
            long tickIndex = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await WaitUntilAsync(nextTimestamp, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                long actualTimestamp = Stopwatch.GetTimestamp();
                tickIndex++;
                long driftTicks = actualTimestamp - nextTimestamp;
                var tick = new HighPrecisionTimerTick(
                    tickIndex,
                    _interval,
                    TimeSpan.FromSeconds((actualTimestamp - startTimestamp) / (double)Stopwatch.Frequency),
                    TimeSpan.FromSeconds(driftTicks / (double)Stopwatch.Frequency),
                    0);

                try
                {
                    _tickAction(tick);
                }
                catch (Exception ex)
                {
                    _errorAction?.Invoke(ex);
                }

                nextTimestamp += intervalTicks;
                long now = Stopwatch.GetTimestamp();
                if (nextTimestamp <= now)
                {
                    long skippedTicks = ((now - nextTimestamp) / intervalTicks) + 1;
                    nextTimestamp += skippedTicks * intervalTicks;
                }
            }
        }

        private static async Task WaitUntilAsync(long targetTimestamp, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                long remainingTicks = targetTimestamp - Stopwatch.GetTimestamp();
                if (remainingTicks <= 0)
                {
                    return;
                }

                double remainingMilliseconds = remainingTicks * 1000.0 / Stopwatch.Frequency;
                if (remainingMilliseconds > 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(remainingMilliseconds - 1), cancellationToken);
                }
                else
                {
                    Thread.SpinWait(64);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HighPrecisionTimer));
            }
        }
    }

    public readonly record struct HighPrecisionTimerTick(
        long TickIndex,
        TimeSpan Interval,
        TimeSpan Elapsed,
        TimeSpan Drift,
        long SkippedTicks);
}
