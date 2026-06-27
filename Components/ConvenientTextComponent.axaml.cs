using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ConvenientText.Models;
using System;

namespace ConvenientText.Components
{
    [ComponentInfo(
        "9E7F8A2D-4C1B-4E5F-9A3C-7D8B2E1F0A3C",
        "便捷文本",
        "\uE9B0",
        "可通过悬浮窗快速修改文字")]
    public partial class ConvenientTextComponent : ComponentBase<TextDataModel>
    {
        private readonly TextBlock _textBlock;
        private TextDataModel? _dataModel;
        private DispatcherTimer? _visibilityTimer;  // 改用 DispatcherTimer
        public static bool IsComponentActive { get; private set; }

        public ConvenientTextComponent()
        {
            _textBlock = new TextBlock
            {
                Text = "点击✎编辑文字",
                Foreground = Avalonia.Media.Brushes.White,
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap
            };
            Content = _textBlock;

            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            _dataModel = Plugin.DataModel;
            if (_dataModel == null)
            {
                System.Diagnostics.Debug.WriteLine("[ConvenientText] 组件加载失败: DataModel 为空");
                return;
            }

            IsComponentActive = true;
            System.Diagnostics.Debug.WriteLine($"[ConvenientText] 组件已加载，当前时间: {DateTime.Now.TimeOfDay}, 是否在时间段内: {_dataModel.IsInTimeRange()}");

            UpdateUI();
            UpdateVisibility();

            _dataModel.PropertyChanged += OnDataModelPropertyChanged;

            // 使用 DispatcherTimer，在 UI 线程直接触发，更可靠
            _visibilityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)  // 5秒检查一次，更灵敏
            };
            _visibilityTimer.Tick += OnTimerTick;
            _visibilityTimer.Start();

            System.Diagnostics.Debug.WriteLine("[ConvenientText] 定时器已启动，每5秒检查一次可见性");
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            IsComponentActive = false;
            if (_dataModel != null)
                _dataModel.PropertyChanged -= OnDataModelPropertyChanged;

            if (_visibilityTimer != null)
            {
                _visibilityTimer.Stop();
                _visibilityTimer.Tick -= OnTimerTick;
                _visibilityTimer = null;
                System.Diagnostics.Debug.WriteLine("[ConvenientText] 定时器已停止");
            }
        }

        private void OnDataModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateUI();
                if (e.PropertyName == nameof(TextDataModel.EnableTimeRange) ||
                    e.PropertyName == nameof(TextDataModel.StartTime) ||
                    e.PropertyName == nameof(TextDataModel.EndTime))
                {
                    System.Diagnostics.Debug.WriteLine($"[ConvenientText] 时间段设置已变更，重新检查可见性");
                    UpdateVisibility();
                }
            });
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            // 已在 UI 线程，直接更新
            UpdateVisibility();
        }

        private void UpdateUI()
        {
            if (_textBlock == null || _dataModel == null) return;
            _textBlock.Text = _dataModel.DisplayText;
            _textBlock.Foreground = new Avalonia.Media.SolidColorBrush(_dataModel.TextColor);
            _textBlock.FontSize = _dataModel.FontSize;
        }

        private void UpdateVisibility()
        {
            if (_dataModel == null) return;

            bool shouldShow = _dataModel.IsInTimeRange();
            bool currentVisible = this.IsVisible;

            if (shouldShow != currentVisible)
            {
                this.IsVisible = shouldShow;
                System.Diagnostics.Debug.WriteLine($"[ConvenientText] 可见性已变更: 当前时间 {DateTime.Now.TimeOfDay}, 显示状态 {shouldShow}");
            }
        }
    }
}