// ============================================================
//  UsbDetectionService.cs
//  作用：U盘插入检测服务（仅 Windows）。
//  通过 WMI（Windows 管理规范）监听“卷变化”事件：一旦检测到有
//  新盘符出现（U盘插入），就弹出一个“防断头”提醒窗口。
//
//  注意：本服务依赖 System.Management 这个 NuGet 包，打包插件时
//  必须把 System.Management.dll 一并放进插件包，否则功能报错。
// ============================================================

using System;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ConvenientText.Views;
using Microsoft.Extensions.Hosting;

namespace ConvenientText.Services
{
    /// <summary>
    /// U盘插入检测服务。
    /// 作为 IHostedService 在插件启动时自动运行（见 Plugin.cs 的注册）。
    /// </summary>
    public class UsbDetectionService : IHostedService, IDisposable
    {
        /// <summary>WMI 事件监听器</summary>
        private ManagementEventWatcher? _watcher;

        /// <summary>防止重复释放资源的标记</summary>
        private bool _disposed;

        /// <summary>
        /// 共享数据存储服务。
        /// 每次插入U盘时【实时】读取提醒开关的状态，
        /// 这样设置页里改开关能立即生效，不用重启。
        /// </summary>
        private readonly DataStorageService _storage;

        /// <summary>
        /// 构造函数。参数由依赖注入自动传入（见 Plugin.cs：
        /// services.AddHostedService&lt;UsbDetectionService&gt;()）。
        /// </summary>
        public UsbDetectionService(DataStorageService storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// 服务启动：创建 WMI 监听器并开始监听“卷到达”事件。
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // WMI 仅在 Windows 上可用，其它系统直接跳过
            if (!OperatingSystem.IsWindows())
                return Task.CompletedTask;

            try
            {
                // Win32_VolumeChangeEvent 的 EventType = 2 表示“新卷到达”（U盘插入）
                var query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
                _watcher = new ManagementEventWatcher(query);
                _watcher.EventArrived += OnUsbInserted;
                _watcher.Start();
                Console.WriteLine("[ConvenientText] USB detection service started.");
            }
            catch (Exception ex)
            {
                // WMI 启动失败（权限不足等）只记日志，不影响插件其它功能
                Console.WriteLine($"[ConvenientText] Failed to start USB detection: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 实时从共享存储读取“U盘插入提醒”开关的状态。
        /// 开关保存在每个有效组件的数据里，取第一个有效组件的值即可。
        /// </summary>
        private bool IsNotificationEnabled()
        {
            try
            {
                var all = _storage.LoadAll();
                var firstValid = all.Values
                    .Where(m => m.IsValid)
                    .OrderBy(m => m.OrderIndex)
                    .FirstOrDefault();
                return firstValid?.EnableUsbNotification ?? true; // 读不到就默认开
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// WMI 事件回调：检测到U盘插入。
        /// 注意：此回调在【后台线程】触发，弹窗前必须切回 UI 线程。
        /// </summary>
        private void OnUsbInserted(object sender, EventArrivedEventArgs e)
        {
            if (!IsNotificationEnabled())
            {
                Console.WriteLine("[ConvenientText] USB notification disabled.");
                return;
            }

            // 切到 UI 线程弹窗（Avalonia 的窗口只能在 UI 线程操作）
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    Console.WriteLine("[ConvenientText] USB drive detected.");
                    var notificationWindow = new UsbNotificationWindow();
                    notificationWindow.Show();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ConvenientText] Error showing USB notification: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 服务停止：关掉监听器。
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    _watcher?.Stop();
                    _watcher?.Dispose();
                }
                catch { }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 释放资源（幂等，重复调用不会出错）。
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _watcher?.Dispose();
                }
                catch { }
                _disposed = true;
            }
        }
    }
}
