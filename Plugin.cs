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
            private FloatingButton? _floatingButton;
            private System.Timers.Timer? _visibilityTimer;
            private PropertyChangedEventHandler? _modelChangedHandler;

            public FloatingWindowHostedService(TextDataModel dataModel, DataStorageService storage)
            {
                _dataModel = dataModel;
                _storage = storage;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                Dispatcher.UIThread.Post(() =>
                {
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

                    _floatingButton = new FloatingButton(_dataModel, _storage);

                    _modelChangedHandler = (sender, e) =>
                    {
                        if (e.PropertyName == nameof(TextDataModel.IsFloatingButtonEnabled) ||
                            e.PropertyName == nameof(TextDataModel.EnableTimeRange) ||
                            e.PropertyName == nameof(TextDataModel.StartTime) ||
                            e.PropertyName == nameof(TextDataModel.EndTime))
                        {
                            UpdateVisibility();
                        }
                    };
                    _dataModel.PropertyChanged += _modelChangedHandler;

                    _visibilityTimer = new System.Timers.Timer(10000);
                    _visibilityTimer.Elapsed += (_, _) => Dispatcher.UIThread.Post(UpdateVisibility);
                    _visibilityTimer.Start();

                    UpdateVisibility();
                    _floatingButton.Show();
                });

                return Task.CompletedTask;
            }

            private void UpdateVisibility()
            {
                if (_floatingButton == null) return;

                bool shouldShow = _dataModel.IsFloatingButtonEnabled &&
                                  ConvenientTextComponent.IsComponentActive &&
                                  _dataModel.IsInTimeRange();

                _floatingButton.IsVisible = shouldShow;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _visibilityTimer?.Stop();
                    _visibilityTimer?.Dispose();
                    if (_modelChangedHandler != null)
                        _dataModel.PropertyChanged -= _modelChangedHandler;
                    _floatingButton?.Close();
                    _floatingButton = null;
                });
                return Task.CompletedTask;
            }
        }
    }
}