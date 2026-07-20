using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Timers;
using Microsoft.Win32;

// 明确使用 System.Timers.Timer，避免与 System.Windows.Forms.Timer 冲突
using Timer = System.Timers.Timer;

namespace ConvenientText.Services
{
    /// <summary>
    /// 时间范围状态服务，负责精确计算并通知“是否在时间段内”的变化。
    /// </summary>
    public class TimeRangeService : INotifyPropertyChanged
    {
        private bool _enableTimeRange;
        private TimeSpan _startTime;
        private TimeSpan _endTime;
        private bool _isInTimeRange;

        private Timer? _timer; // 单次定时器
        private readonly object _lock = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool EnableTimeRange
        {
            get => _enableTimeRange;
            set
            {
                if (_enableTimeRange == value) return;
                _enableTimeRange = value;
                OnPropertyChanged(nameof(EnableTimeRange));
                RecalculateTimer();
            }
        }

        public TimeSpan StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime == value) return;
                _startTime = value;
                OnPropertyChanged(nameof(StartTime));
                RecalculateTimer();
            }
        }

        public TimeSpan EndTime
        {
            get => _endTime;
            set
            {
                if (_endTime == value) return;
                _endTime = value;
                OnPropertyChanged(nameof(EndTime));
                RecalculateTimer();
            }
        }

        public bool IsInTimeRange
        {
            get => _isInTimeRange;
            private set
            {
                if (_isInTimeRange == value) return;
                _isInTimeRange = value;
                OnPropertyChanged(nameof(IsInTimeRange));
            }
        }

        public TimeRangeService()
        {
            IsInTimeRange = ComputeIsInTimeRange();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SystemEvents.TimeChanged += (_, _) => RecalculateTimer();
            }
        }

        public void UpdateSettings(bool enable, TimeSpan start, TimeSpan end)
        {
            lock (_lock)
            {
                _enableTimeRange = enable;
                _startTime = start;
                _endTime = end;
                RecalculateTimerLocked();
            }
        }

        private void RecalculateTimer()
        {
            lock (_lock)
            {
                RecalculateTimerLocked();
            }
        }

        private void RecalculateTimerLocked()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            if (!_enableTimeRange)
            {
                IsInTimeRange = true;
                return;
            }

            bool current = ComputeIsInTimeRange();
            IsInTimeRange = current;

            DateTime now = DateTime.Now;
            TimeSpan nowTime = now.TimeOfDay;
            DateTime nextEvent;

            if (current)
            {
                DateTime todayEnd = now.Date.Add(_endTime);
                if (_endTime > _startTime)
                    nextEvent = nowTime < _endTime ? todayEnd : todayEnd.AddDays(1);
                else
                    nextEvent = (nowTime >= _startTime || nowTime < _endTime)
                        ? (nowTime < _endTime ? todayEnd : todayEnd.AddDays(1))
                        : todayEnd;
                if (nextEvent <= now) nextEvent = nextEvent.AddSeconds(1);
            }
            else
            {
                DateTime todayStart = now.Date.Add(_startTime);
                if (_startTime > _endTime)
                {
                    if (nowTime >= _endTime && nowTime < _startTime)
                        nextEvent = todayStart;
                    else
                        nextEvent = todayStart.AddDays(1);
                }
                else
                {
                    nextEvent = nowTime < _startTime ? todayStart : todayStart.AddDays(1);
                }
                if (nextEvent <= now) nextEvent = nextEvent.AddSeconds(1);
            }

            double interval = (nextEvent - now).TotalMilliseconds;
            if (interval < 0) interval = 0;

            _timer = new Timer(interval);
            _timer.Elapsed += (s, e) =>
            {
                lock (_lock)
                {
                    IsInTimeRange = ComputeIsInTimeRange();
                    RecalculateTimerLocked();
                }
            };
            _timer.AutoReset = false;
            _timer.Start();
        }

        private bool ComputeIsInTimeRange()
        {
            if (!_enableTimeRange) return true;
            var now = DateTime.Now.TimeOfDay;
            if (_startTime <= _endTime)
                return now >= _startTime && now <= _endTime;
            else
                return now >= _startTime || now <= _endTime;
        }

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}