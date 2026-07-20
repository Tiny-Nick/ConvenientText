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
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace ConvenientText
{
    [PluginEntrance]
    public class Plugin : PluginBase
    {
        public static IServiceProvider? ServiceProvider { get; internal set; }
        public static TextDataModel? DataModel { get; private set; }
        public static DataStorageService? Storage { get; private set; }

        public override void Initialize(HostBuilderContext context, IServiceCollection services)
        {
            services.AddSingleton<TextDataModel>();
            services.AddSingleton<DataStorageService>();
            services.AddSingleton<TimeRangeService>();
            services.AddHostedService<UsbDetectionService>(provider =>
                new UsbDetectionService(provider.GetRequiredService<TextDataModel>()));
            services.AddHostedService<ServiceProviderHolder>();
            services.AddComponent<ConvenientTextComponent, ConvenientTextSettingsControl>();
            services.AddHostedService<FloatingWindowHostedService>();

            services.AddSettingsPage<PluginSettingsControl>();
        }

        private class ServiceProviderHolder : IHostedService
        {
            private readonly IServiceProvider _serviceProvider;

            public ServiceProviderHolder(IServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                Plugin.ServiceProvider = _serviceProvider;
                Plugin.DataModel = _serviceProvider.GetRequiredService<TextDataModel>();
                Plugin.Storage = _serviceProvider.GetRequiredService<DataStorageService>();
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private class FloatingWindowHostedService : IHostedService
        {
            private readonly TextDataModel _dataModel;
            private readonly DataStorageService _storage;
            private readonly TimeRangeService _timeRangeService;
            private FloatingButton? _floatingButton;
            private PropertyChangedEventHandler? _modelChangedHandler;

            public FloatingWindowHostedService(TextDataModel dataModel, DataStorageService storage, TimeRangeService timeRangeService)
            {
                _dataModel = dataModel;
                _storage = storage;
                _timeRangeService = timeRangeService;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // 加载保存的数据
                    var saved = _storage.Load();
                    _dataModel.DisplayText = saved.DisplayText;
                    _dataModel.TextColor = saved.TextColor;
                    _dataModel.FontSize = saved.FontSize;
                    _dataModel.FloatingX = saved.FloatingX;
                    _dataModel.FloatingY = saved.FloatingY;
                    _dataModel.IsFloatingButtonEnabled = saved.IsFloatingButtonEnabled;
                    _dataModel.EnableTimeRange = saved.EnableTimeRange;
                    _dataModel.StartTime = saved.StartTime;
                    _dataModel.EndTime = saved.EndTime;
                    _dataModel.EnableUsbNotification = saved.EnableUsbNotification;

                    // 同步 TimeRangeService 的设置
                    _timeRangeService.UpdateSettings(_dataModel.EnableTimeRange, _dataModel.StartTime, _dataModel.EndTime);

                    _floatingButton = new FloatingButton(_dataModel, _storage);

                    // 监听数据模型变化（时间范围、启用开关）
                    _modelChangedHandler = (sender, e) =>
                    {
                        if (e.PropertyName == nameof(TextDataModel.IsFloatingButtonEnabled) ||
                            e.PropertyName == nameof(TextDataModel.EnableTimeRange) ||
                            e.PropertyName == nameof(TextDataModel.StartTime) ||
                            e.PropertyName == nameof(TextDataModel.EndTime))
                        {
                            // 当时间范围设置变化时，同步到 TimeRangeService
                            if (e.PropertyName == nameof(TextDataModel.EnableTimeRange) ||
                                e.PropertyName == nameof(TextDataModel.StartTime) ||
                                e.PropertyName == nameof(TextDataModel.EndTime))
                            {
                                _timeRangeService.UpdateSettings(_dataModel.EnableTimeRange, _dataModel.StartTime, _dataModel.EndTime);
                            }
                            UpdateVisibility();
                        }
                    };
                    _dataModel.PropertyChanged += _modelChangedHandler;

                    // 订阅组件激活状态变化事件
                    ConvenientTextComponent.ActiveChanged += OnComponentActiveChanged;

                    // 订阅时间范围服务的状态变化
                    _timeRangeService.PropertyChanged += OnTimeRangeChanged;

                    // 初始更新可见性
                    UpdateVisibility();
                    _floatingButton.Show();
                });

                return Task.CompletedTask;
            }

            private void OnComponentActiveChanged(object? sender, bool active)
            {
                Dispatcher.UIThread.Post(UpdateVisibility);
            }

            private void OnTimeRangeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(TimeRangeService.IsInTimeRange))
                {
                    Dispatcher.UIThread.Post(UpdateVisibility);
                }
            }

            private void UpdateVisibility()
            {
                if (_floatingButton == null) return;

                bool shouldShow = _dataModel.IsFloatingButtonEnabled &&
                                  ConvenientTextComponent.IsComponentActive &&
                                  _timeRangeService.IsInTimeRange;

                _floatingButton.IsVisible = shouldShow;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_modelChangedHandler != null)
                        _dataModel.PropertyChanged -= _modelChangedHandler;
                    ConvenientTextComponent.ActiveChanged -= OnComponentActiveChanged;
                    if (_timeRangeService != null)
                        _timeRangeService.PropertyChanged -= OnTimeRangeChanged;
                    _floatingButton?.Close();
                    _floatingButton = null;
                });
                return Task.CompletedTask;
            }
        }
    }
}