using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace ConvenientText.Views
{
    public partial class UsbNotificationWindow : Window
    {
        private readonly System.Timers.Timer? _autoCloseTimer;

        public UsbNotificationWindow()
        {
            InitializeComponent();

            _autoCloseTimer = new System.Timers.Timer(10000);
            _autoCloseTimer.Elapsed += (s, e) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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