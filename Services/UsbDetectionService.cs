using Avalonia.Threading;
using ConvenientText.Models;
using ConvenientText.Views;
using Microsoft.Extensions.Hosting;
using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace ConvenientText.Services
{
    public class UsbDetectionService : IHostedService, IDisposable
    {
        private ManagementEventWatcher? _watcher;
        private bool _disposed;
        private readonly TextDataModel? _dataModel;

        public UsbDetectionService(TextDataModel? dataModel = null)
        {
            _dataModel = dataModel;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
                _watcher = new ManagementEventWatcher(query);
                _watcher.EventArrived += OnUsbInserted;
                _watcher.Start();
                Console.WriteLine("[ConvenientText] USB detection service started.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConvenientText] Failed to start USB detection: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        private void OnUsbInserted(object sender, EventArrivedEventArgs e)
        {
            // 用 UI 线程弹出窗口
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    Console.WriteLine("[ConvenientText] USB drive detected.");

                    if (_dataModel != null && !_dataModel.EnableUsbNotification)
                    {
                        Console.WriteLine("[ConvenientText] USB notification disabled.");
                        return;
                    }

                    // 弹出同款样式的提示窗口
                    var notificationWindow = new UsbNotificationWindow();
                    notificationWindow.Show();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ConvenientText] Error: {ex.Message}");
                }
            });
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _watcher?.Stop();
            _watcher?.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _watcher?.Dispose();
                _disposed = true;
            }
        }
    }
}