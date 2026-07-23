// ============================================================
//  UsbNotificationWindow.axaml.cs
//  作用：U盘插入时弹出的“防断头”提醒窗口。
//  显示 10 秒后自动关闭，也可以手动点确定/关闭提前关掉。
// ============================================================

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ConvenientText.Views
{
    public partial class UsbNotificationWindow : Window
    {
        private readonly System.Timers.Timer? _autoCloseTimer;

        public UsbNotificationWindow()
        {
            InitializeComponent();

            AcrylicTitleBarHelper.Attach(this);

            _autoCloseTimer = new System.Timers.Timer(10000);
            _autoCloseTimer.Elapsed += (s, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    this.Close();
                });
                _autoCloseTimer.Stop();
            };
            _autoCloseTimer.Start();
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            _autoCloseTimer?.Stop();
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoCloseTimer?.Stop();
            _autoCloseTimer?.Dispose();
            base.OnClosed(e);
        }
    }
}
