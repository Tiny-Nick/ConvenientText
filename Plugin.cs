using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ConvenientText.Components;
using ConvenientText.Models;
using ConvenientText.Services;
using ConvenientText.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace ConvenientText;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 注册数据模型、存储服务、组件
        services.AddSingleton<TextDataModel>();
        services.AddSingleton<DataStorageService>();
        services.AddComponent<ConvenientTextComponent, ConvenientTextSettingsControl>();

        // 注册托管服务，用来启动和停止悬浮窗
        services.AddHostedService<FloatingWindowHostedService>();
    }

    // 内部类：实现 IHostedService
    private class FloatingWindowHostedService : IHostedService
    {
        private readonly TextDataModel _dataModel;
        private readonly DataStorageService _storage;
        private FloatingButton? _floatingButton;

        public FloatingWindowHostedService(TextDataModel dataModel, DataStorageService storage)
        {
            _dataModel = dataModel;
            _storage = storage;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Dispatcher.UIThread.Post(() =>
            {
                // 1. 加载保存的数据
                var saved = _storage.Load();
                _dataModel.DisplayText = saved.DisplayText;
                _dataModel.TextColor = saved.TextColor;
                _dataModel.FontSize = saved.FontSize;

                // 2. 创建悬浮窗
                _floatingButton = new FloatingButton(_dataModel, _storage);

                // 3. 跟随主窗口的显示和置顶状态
                var mainWindow = AppBase.Current?.MainWindow;
                if (mainWindow != null)
                {
                    void OnMainWindowPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
                    {
                        if (_floatingButton == null) return;
                        if (e.Property == Window.IsVisibleProperty)
                            _floatingButton.IsVisible = mainWindow.IsVisible;
                        else if (e.Property == Window.TopmostProperty)
                            _floatingButton.Topmost = mainWindow.Topmost;
                    }

                    mainWindow.PropertyChanged += OnMainWindowPropertyChanged;
                    // 注意：这里不保存订阅，因为插件卸载时会关闭窗口，且事件订阅会随主窗口一起释放
                    // 如果担心内存泄漏，可以在 StopAsync 中手动移除，但 ClassIsland 卸载插件时会清理，所以安全
                }

                // 4. 设置初始位置（左上角）
                var screen = _floatingButton.Screens.Primary;
                if (screen != null)
                {
                    _floatingButton.Position = new PixelPoint(20, 100);
                }

                // 5. 显示悬浮窗
                _floatingButton.Show();
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _floatingButton?.Close();
                _floatingButton = null;
            });
            return Task.CompletedTask;
        }
    }
}