// ============================================================
//  TimeRangeService.cs
//  作用：全插件共用的“心跳时钟”。
//  每 5 秒钟更新一次当前时间并广播，组件和悬浮按钮靠它来
//  判断“现在是否处于用户设定的时间段内”，从而自动显示/隐藏。
// ============================================================

using System;
using System.ComponentModel;

namespace ConvenientText.Services
{
    /// <summary>
    /// 时间广播服务。
    /// 内部开一个定时器，每 5 秒把 CurrentTime 刷新为当前时间，
    /// 并通过 INotifyPropertyChanged 通知所有订阅者。
    /// 在 Plugin.cs 里注册为单例（AddSingleton），整个插件共享一个实例。
    /// </summary>
    public class TimeRangeService : INotifyPropertyChanged
    {
        /// <summary>当前时间（每 5 秒刷新一次）</summary>
        private DateTime _currentTime = DateTime.Now;

        /// <summary>后台定时器：负责周期性地刷新 _currentTime</summary>
        private readonly System.Timers.Timer _updateTimer;

        /// <summary>属性变化事件，界面/组件订阅它来收到“时间变了”的通知</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        public TimeRangeService()
        {
            // 5000 毫秒 = 5 秒刷新一次。
            // 注意：System.Timers.Timer 的 Elapsed 在【后台线程】触发，
            // 所以订阅者在收到通知后如果要操作界面，必须切回 UI 线程
            // （各订阅处都用 Dispatcher.UIThread.Post 处理了）。
            _updateTimer = new System.Timers.Timer(5000);
            _updateTimer.Elapsed += (s, e) =>
            {
                CurrentTime = DateTime.Now;
            };
            _updateTimer.Start();
        }

        /// <summary>
        /// 当前时间。外部只读，只有定时器能改。
        /// </summary>
        public DateTime CurrentTime
        {
            get => _currentTime;
            private set
            {
                if (_currentTime == value) return; // 值没变就不通知，减少无谓刷新
                _currentTime = value;
                OnPropertyChanged(nameof(CurrentTime));
            }
        }

        /// <summary>
        /// 当前时刻（一天内的时间，不含日期）。
        /// 组件的时间段判断用的就是它，例如 8:00 ~ 22:00。
        /// </summary>
        public TimeSpan NowTimeOfDay => CurrentTime.TimeOfDay;

        /// <summary>触发属性变化通知</summary>
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
