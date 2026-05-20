using System;
using System.Threading;

namespace Framework.TimerManager
{
    public class UniqueTimer
    {
        public string Name { get; }
        private readonly int _intervalMilliseconds;
        private readonly int _totalDurationMilliseconds;
        private readonly Action<int>? _intervalCallBack;
        private readonly Action? _endCallBack;
        private Timer? _internalTimer;
        private CancellationTokenSource? _cancellationTokenSource;

        private int _elapsedMilliseconds = 0;
        private bool _isPaused = false;

        public bool IsRunning => _internalTimer != null && !_isPaused;
        public bool IsPaused => _isPaused;

        public UniqueTimer(int intervalMilliseconds, int totalDurationSeconds = -1, Action<int>? intervalCallback = null, Action? endCallback = null, int fineness = 1)
        {
            Name = Guid.NewGuid().ToString();
            _intervalMilliseconds = intervalMilliseconds * fineness;
            _totalDurationMilliseconds = totalDurationSeconds == -1 ? int.MaxValue : totalDurationSeconds;
            _intervalCallBack = intervalCallback;
            _endCallBack = endCallback;
        }

        public void Start()
        {
            if (_internalTimer != null)
                return;

            _isPaused = false;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            _internalTimer = new Timer(_ =>
            {
                if (!token.IsCancellationRequested && !_isPaused)
                {
                    _elapsedMilliseconds += _intervalMilliseconds;
                    _intervalCallBack?.Invoke(_elapsedMilliseconds);

                    if (_elapsedMilliseconds >= _totalDurationMilliseconds)
                    {
                        _endCallBack?.Invoke();
                        Stop();
                    }

                    // 防止 int 溢出
                    if (_elapsedMilliseconds > int.MaxValue - 2 * _intervalMilliseconds)
                    {
                        _elapsedMilliseconds = 0;
                    }
                }
            }, null, 0, _intervalMilliseconds);
        }

        public void Pause()
        {
            if (_internalTimer != null && !_isPaused)
            {
                _isPaused = true;
                _internalTimer?.Change(Timeout.Infinite, Timeout.Infinite); // 暂停定时器
            }
        }

        public void Resume()
        {
            if (_internalTimer != null && _isPaused)
            {
                _isPaused = false;
                _internalTimer?.Change(0, _intervalMilliseconds); // 恢复定时器
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _internalTimer?.Dispose();
            _internalTimer = null;
            _cancellationTokenSource = null;
            _elapsedMilliseconds = 0;
            _isPaused = false;
        }
    }
}
