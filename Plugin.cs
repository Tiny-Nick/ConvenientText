// ============================================================
//  Plugin.cs
//  作用：插件入口与生命周期管理。
//  ClassIsland 启动时调用 Initialize()，在这里注册：
//    · 单例服务：DataStorageService（数据中心）、TimeRangeService（时钟）
//    · 后台服务：UsbDetectionService（U盘检测）、FloatingWindowHostedService（悬浮按钮）
//    · 组件：便捷文本组件 + 组件设置控件
//    · 设置页：便捷文本设置页
//
//  还暴露两个静态属性（ServiceProvider / Storage），方便
//  各个窗口在不方便依赖注入的地方拿到服务。
// ============================================================

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

namespace ConvenientText
{
    [PluginEntrance]
    public class Plugin : PluginBase
    {
        /// <summary>全局服务提供者，方便不便依赖注入的地方取服务（由 ServiceProviderHolder 赋值）</summary>
        public static IServiceProvider? ServiceProvider { get; internal set; }

        /// <summary>共享数据存储服务的快捷访问入口（由 ServiceProviderHolder 赋值）</summary>
        public static DataStorageService? Storage { get; private set; }

        /// <summary>
        /// 插件初始化：ClassIsland 加载插件时调用，在这里注册所有服务/组件/设置页。
        /// </summary>
        public override void Initialize(HostBuilderContext context, IServiceCollection services)
        {
            // ----- 单例服务：整个插件共享一个实例 -----
            services.AddSingleton<DataStorageService>();  // 数据中心：读写 data.json
            services.AddSingleton<TimeRangeService>();    // 心跳时钟：时间段显示判断

            // U盘插入检测服务（后台运行；依赖注入自动传入 DataStorageService）
            services.AddHostedService<UsbDetectionService>();

            // 把 ServiceProvider 存到静态属性上（必须在最先启动，别的服务要用）
            services.AddHostedService<ServiceProviderHolder>();

            // 注册“便捷文本”组件和它的组件设置控件
            services.AddComponent<ConvenientTextComponent, ConvenientTextSettingsControl>();

            // 桌面悬浮按钮的宿主服务（负责创建/显隐悬浮按钮）
            services.AddHostedService<FloatingWindowHostedService>();

            // 注册插件设置页（ClassIsland 设置窗口里的“便捷文本”页）
            services.AddSettingsPage<PluginSettingsControl>();
        }

        /// <summary>
        /// 一个“借尸还魂”的小服务：IHostedService 能拿到依赖注入的
        /// IServiceProvider，趁启动时把它存到 Plugin 的静态属性上，
        /// 这样任何代码都能通过 Plugin.Storage / Plugin.ServiceProvider
        /// 拿到核心服务，而不必层层构造函数传递。
        /// </summary>
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
                Plugin.Storage = _serviceProvider.GetRequiredService<DataStorageService>();
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        /// <summary>
        /// 悬浮按钮宿主服务。
        /// 【修复】旧版订阅了组件的静态事件并形成循环触发（事件风暴），
        /// 现在只监听三个明确的信号：存储数据变化、组件加载/卸载、时间变化。
        /// </summary>
        private class FloatingWindowHostedService : IHostedService
        {
            private readonly DataStorageService _storage;
            private readonly TimeRangeService _timeRangeService;
            private FloatingButton? _floatingButton;
            private TextDataModel? _displayModel;
            private bool _isDisposed = false;

            public FloatingWindowHostedService(DataStorageService storage, TimeRangeService timeRangeService)
            {
                _storage = storage;
                _timeRangeService = timeRangeService;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        RefreshDisplayModel();

                        // 没有可用组件时也先创建一个占位模型，保证按钮能初始化
                        _floatingButton = new FloatingButton(
                            _displayModel ?? TextDataModel.CreateNew(1, Avalonia.Media.Colors.Gray),
                            _storage);

                        _storage.DataChanged += OnDataOrComponentsChanged;
                        ConvenientTextComponent.LiveModelsChanged += OnDataOrComponentsChanged;
                        _timeRangeService.PropertyChanged += OnTimeChanged;

                        _floatingButton.Show();
                        UpdateFloatingButtonVisibility();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ConvenientText] FloatingButton startup error: {ex.Message}");
                    }
                });

                return Task.CompletedTask;
            }

            private void OnDataOrComponentsChanged(object? sender, EventArgs e)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        RefreshDisplayModel();
                        UpdateFloatingButtonVisibility();
                    }
                    catch { }
                });
            }

            private void OnTimeChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(TimeRangeService.CurrentTime))
                {
                    Dispatcher.UIThread.Post(UpdateFloatingButtonVisibility);
                }
            }

            /// <summary>
            /// 挑选悬浮按钮要绑定的组件：
            /// 优先沿用当前绑定的组件；否则取主界面上第一个有效组件。
            /// </summary>
            private void RefreshDisplayModel()
            {
                var all = _storage.LoadAll();
                var live = ConvenientTextComponent.LiveModels;

                TextDataModel? pick = null;

                if (_displayModel != null &&
                    live.TryGetValue(_displayModel.ComponentId, out var current) &&
                    current.IsValid)
                {
                    pick = all.TryGetValue(current.ComponentId, out var stored) ? stored : current;
                }
                else
                {
                    var firstLive = live.Values
                        .Where(m => m.IsValid)
                        .OrderBy(m => m.OrderIndex)
                        .FirstOrDefault();
                    if (firstLive != null)
                    {
                        pick = all.TryGetValue(firstLive.ComponentId, out var stored) ? stored : firstLive;
                    }
                }

                if (pick != null)
                {
                    _displayModel = pick;
                    _floatingButton?.UpdateDataModel(pick);
                }
            }

            private void UpdateFloatingButtonVisibility()
            {
                if (_floatingButton == null) return;

                bool shouldShow = _displayModel != null &&
                                  _displayModel.IsValid &&
                                  _displayModel.IsFloatingButtonEnabled &&
                                  ConvenientTextComponent.LiveModels.ContainsKey(_displayModel.ComponentId) &&
                                  _displayModel.IsInTimeRange(_timeRangeService.NowTimeOfDay);

                _floatingButton.IsVisible = shouldShow;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_isDisposed) return;
                    _isDisposed = true;

                    _storage.DataChanged -= OnDataOrComponentsChanged;
                    ConvenientTextComponent.LiveModelsChanged -= OnDataOrComponentsChanged;
                    _timeRangeService.PropertyChanged -= OnTimeChanged;

                    try
                    {
                        _floatingButton?.Close();
                    }
                    catch { }
                    _floatingButton = null;
                });

                return Task.CompletedTask;
            }
        }
    }
}
