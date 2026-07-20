using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ConvenientText.Models;
using ConvenientText.Services;

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
        private TimeRangeService? _timeRangeService;
        private bool _isActive;

        // 静态事件：通知外部组件激活状态变化
        public static event EventHandler<bool>? ActiveChanged;

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

            _timeRangeService = Plugin.ServiceProvider?.GetService(typeof(TimeRangeService)) as TimeRangeService;
            if (_timeRangeService != null)
            {
                _timeRangeService.PropertyChanged += OnTimeRangeChanged;
                UpdateVisibilityFromService();
            }

            // 订阅数据模型变化（用于更新文本和颜色）
            _dataModel.PropertyChanged += OnDataModelPropertyChanged;

            SetActive(true);
            UpdateUI();
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_timeRangeService != null)
                _timeRangeService.PropertyChanged -= OnTimeRangeChanged;
            if (_dataModel != null)
                _dataModel.PropertyChanged -= OnDataModelPropertyChanged;
            SetActive(false);
        }

        private void SetActive(bool active)
        {
            if (_isActive == active) return;
            _isActive = active;
            IsComponentActive = active;
            ActiveChanged?.Invoke(this, active);
        }

        private void OnTimeRangeChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TimeRangeService.IsInTimeRange))
            {
                Dispatcher.UIThread.Post(UpdateVisibilityFromService);
            }
        }

        private void UpdateVisibilityFromService()
        {
            if (_timeRangeService == null) return;
            bool shouldShow = _timeRangeService.IsInTimeRange;
            if (this.IsVisible != shouldShow)
                this.IsVisible = shouldShow;
        }

        private void OnDataModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateUI();
                // 注意：时间范围变化现在由 TimeRangeService 处理，无需在此判断
            });
        }

        private void UpdateUI()
        {
            if (_textBlock == null || _dataModel == null) return;
            _textBlock.Text = _dataModel.DisplayText;
            _textBlock.Foreground = new Avalonia.Media.SolidColorBrush(_dataModel.TextColor);
            _textBlock.FontSize = _dataModel.FontSize;
        }
    }
}