// ============================================================
//  ConvenientTextComponent.axaml.cs
//  作用：显示在 ClassIsland 主界面上的“便捷文本”组件本体。
//  界面是代码动态创建的（圆点 + 文字），没有对应的 .axaml 文件。
//
//  职责：
//    1. 加载时给新组件分配身份（ComponentId/序号/圆点颜色）；
//    2. 把组件设置注册进共享存储（data.json）；
//    3. 监听存储变化并同步显示（别的窗口改了这里跟着变）；
//    4. 根据时间段自动显示/隐藏内容；
//    5. 左键点击弹出“编辑文本”窗口。
//
//  注意：组件自己的 Settings（由 ClassIsland 保存）是显示的
//  权威数据源，data.json 是各窗口共享的副本，两者通过
//  DataChanged 广播 + CopyFrom() 保持同步。
// ============================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ConvenientText.Models;
using ConvenientText.Services;
using ConvenientText.Views;
using Microsoft.Extensions.DependencyInjection;

// 消除歧义别名
using AvaloniaStackPanel = Avalonia.Controls.StackPanel;
using AvaloniaTextBlock = Avalonia.Controls.TextBlock;
using Brushes = Avalonia.Media.Brushes;

namespace ConvenientText.Components
{
    [ComponentInfo(
        "9E7F8A2D-4C1B-4E5F-9A3C-7D8B2E1F0A3C",
        "便捷文本",
        "\uE9B0",
        "可通过悬浮窗快速修改文字")]
    public partial class ConvenientTextComponent : ComponentBase<TextDataModel>
    {
        private readonly AvaloniaStackPanel _mainPanel;
        private readonly AvaloniaTextBlock _dotTextBlock;
        private readonly AvaloniaTextBlock _textBlock;
        private TextDataModel? _dataModel;
        private DataStorageService? _storage;
        private TimeRangeService? _timeRangeService;
        private bool _isLoaded = false;
        private bool _isOverflow = false; // 超过 5 个组件上限时仅显示提示，不注册

        // ============================================================
        //  【修复】当前真正加载在主界面上的组件登记表
        //  旧版用静态事件 ComponentListChanged 互相通知，处理函数里又
        //  重新触发该事件，形成指数级事件风暴，导致“添加组件 CI 直接
        //  原地爆炸”。现在改为：只在组件加载/卸载时各广播一次，且
        //  广播内容里绝不再触发自身。
        // ============================================================
        private static readonly Dictionary<string, TextDataModel> _liveModels = new();
        public static IReadOnlyDictionary<string, TextDataModel> LiveModels => _liveModels;
        public static event EventHandler? LiveModelsChanged;

        /// <summary>
        /// 构造函数：用代码搭建组件界面（圆点 + 文字 的横向排列）。
        /// 组件没有 .axaml 文件，界面全部在这里创建。
        /// </summary>
        public ConvenientTextComponent()
        {
            // 横向容器：圆点 + 文字
            _mainPanel = new AvaloniaStackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Spacing = 6
            };

            // 左侧的标识圆点（颜色代表组件编号）
            _dotTextBlock = new AvaloniaTextBlock
            {
                Text = "●",
                FontSize = 20,
                Foreground = Brushes.Gray,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            // 正文文字（初始显示“加载中”，OnLoaded 后换成真实内容）
            _textBlock = new AvaloniaTextBlock
            {
                Text = "加载中...",
                Foreground = Brushes.White,
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };

            _mainPanel.Children.Add(_dotTextBlock);
            _mainPanel.Children.Add(_textBlock);
            Content = _mainPanel; // 把面板挂到组件上显示

            // 订阅三个事件：点击（弹编辑窗）、加载完成（初始化数据）、卸载（清理）
            this.PointerPressed += OnComponentPointerPressed;
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        // ============================================================
        //  点击组件弹出编辑窗口
        // ============================================================
        private void OnComponentPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_dataModel == null || _isOverflow || !_dataModel.IsValid) return;

            // 【修复】只响应左键，避免右键/拖拽时误触
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

            var editWindow = new EditTextWindow(_dataModel);
            editWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            editWindow.Show();
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;

            _storage = Plugin.Storage ?? new DataStorageService();
            _timeRangeService = Plugin.ServiceProvider?.GetService<TimeRangeService>()
                                ?? new TimeRangeService();

            _dataModel = this.Settings;
            if (_dataModel == null)
            {
                System.Diagnostics.Debug.WriteLine("[ConvenientText] 组件加载失败: Settings 为空");
                return;
            }

            if (string.IsNullOrEmpty(_dataModel.ComponentId))
            {
                // ===== 这是一个新添加的组件，还没有身份 =====
                int liveValidCount = _liveModels.Values.Count(m => m.IsValid);
                if (liveValidCount >= DataStorageService.MAX_COMPONENTS)
                {
                    // 超过上限：只显示提示，不写存储、不订阅事件
                    _isOverflow = true;
                    ShowOverflowUI();
                    return;
                }

                // 【修复】给它分配稳定的 ComponentId，旧版这里是空字符串，
                // 导致所有组件共用同一个 "" 键互相覆盖数据。
                _dataModel.ComponentId = Guid.NewGuid().ToString();
                _dataModel.OrderIndex = NextOrderIndex();
                _dataModel.DotColor = _storage.GetNextColor(BuildColorContext());
                _dataModel.IsValid = true;
                RegisterToStorage();
            }
            else
            {
                // ===== 已有身份的组件（重启后恢复）=====
                var all = _storage.LoadAll();
                if (all.TryGetValue(_dataModel.ComponentId, out var stored))
                {
                    if (!stored.IsValid)
                    {
                        // 存储里被标记为无效（比如超出上限的历史数据），按溢出处理
                        _isOverflow = true;
                        ShowOverflowUI();
                        return;
                    }
                    // 以共享存储为准同步一次（其它窗口可能在我们没加载时改过）
                    _dataModel.CopyFrom(stored);
                }
                else
                {
                    RegisterToStorage();
                }
            }

            // 登记到“已加载组件”表，供悬浮按钮和设置页感知真实组件列表
            _liveModels[_dataModel.ComponentId] = _dataModel;
            LiveModelsChanged?.Invoke(null, EventArgs.Empty);

            _dataModel.PropertyChanged += OnDataModelPropertyChanged;
            _storage.DataChanged += OnStorageDataChanged;
            if (_timeRangeService != null)
                _timeRangeService.PropertyChanged += OnTimeChanged;

            UpdateUI();
            UpdateVisibility();
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_dataModel != null)
            {
                _dataModel.PropertyChanged -= OnDataModelPropertyChanged;
                if (!string.IsNullOrEmpty(_dataModel.ComponentId))
                {
                    _liveModels.Remove(_dataModel.ComponentId);
                    LiveModelsChanged?.Invoke(null, EventArgs.Empty);
                }
            }
            if (_storage != null)
                _storage.DataChanged -= OnStorageDataChanged;
            if (_timeRangeService != null)
                _timeRangeService.PropertyChanged -= OnTimeChanged;

            // 【修复】允许重新加载（主界面关闭再打开时 Loaded 会再次触发）
            _isLoaded = false;
            _isOverflow = false;
        }

        // ============================================================
        //  数据同步
        // ============================================================

        /// <summary>
        /// 共享存储(data.json)被其它窗口修改后，同步到本组件
        /// </summary>
        private void OnStorageDataChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_dataModel == null || _storage == null || _isOverflow) return;
                    if (string.IsNullOrEmpty(_dataModel.ComponentId)) return;

                    var all = _storage.LoadAll();
                    if (all.TryGetValue(_dataModel.ComponentId, out var stored) && stored.IsValid)
                    {
                        _dataModel.CopyFrom(stored);
                        UpdateUI();
                        UpdateVisibility();
                    }
                }
                catch { }
            });
        }

        private void OnDataModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateUI();
                if (e.PropertyName == nameof(TextDataModel.EnableTimeRange) ||
                    e.PropertyName == nameof(TextDataModel.StartTime) ||
                    e.PropertyName == nameof(TextDataModel.EndTime))
                {
                    UpdateVisibility();
                }
            });
        }

        private void OnTimeChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TimeRangeService.CurrentTime))
                Dispatcher.UIThread.Post(UpdateVisibility);
        }

        // ============================================================
        //  界面更新
        // ============================================================

        private void UpdateUI()
        {
            if (_dataModel == null || _textBlock == null) return;
            if (_isOverflow || !_dataModel.IsValid) return;

            _textBlock.Text = _dataModel.DisplayText;
            _textBlock.Foreground = new SolidColorBrush(_dataModel.TextColor);
            _textBlock.FontSize = _dataModel.FontSize;
            _dotTextBlock.Foreground = new SolidColorBrush(_dataModel.DotColor);
            _dotTextBlock.Text = "●";
        }

        private void UpdateVisibility()
        {
            if (_dataModel == null) return;
            if (_isOverflow || !_dataModel.IsValid)
            {
                _mainPanel.IsVisible = true; // 溢出/无效组件显示提示信息
                return;
            }

            // 【关键修复】绝不能设置 this.IsVisible（组件控件本身的可见性）！
            // ClassIsland 的组件容器对控件可见性变化很敏感，隐藏组件控件会
            // 触发“卸载→重新加载→再次隐藏”的死循环，直接把界面线程卡死
            // （未响应）。改为只隐藏组件内部的内容面板，组件外壳保持原样。
            var currentTime = _timeRangeService?.NowTimeOfDay ?? DateTime.Now.TimeOfDay;
            bool shouldShow = _dataModel.IsInTimeRange(currentTime);
            if (_mainPanel.IsVisible != shouldShow)
                _mainPanel.IsVisible = shouldShow;
        }

        private void ShowOverflowUI()
        {
            _dotTextBlock.Text = "⚠";
            _dotTextBlock.Foreground = Brushes.Gray;
            _textBlock.Text = "哎呀，最多添加5个文本组件";
            _textBlock.Foreground = Brushes.Gray;
            _textBlock.FontSize = 14;
            _mainPanel.IsVisible = true;
        }

        // ============================================================
        //  工具方法
        // ============================================================

        private void RegisterToStorage()
        {
            if (_storage == null || _dataModel == null) return;
            var all = _storage.LoadAll();
            // 【修复】存克隆体而不是本体，避免别的窗口直接改到 Settings
            all[_dataModel.ComponentId] = _dataModel.Clone();
            _storage.SaveAll(all);
        }

        private int NextOrderIndex()
        {
            int max = 0;
            foreach (var m in _liveModels.Values)
                if (m.IsValid && m.OrderIndex > max) max = m.OrderIndex;
            return max + 1;
        }

        /// <summary>
        /// 汇总“已加载组件 + 存储中的组件”，用于分配不重复的颜色
        /// </summary>
        private Dictionary<string, TextDataModel> BuildColorContext()
        {
            var context = _storage?.LoadAll() ?? new Dictionary<string, TextDataModel>();
            foreach (var kv in _liveModels)
            {
                if (!context.ContainsKey(kv.Key))
                    context[kv.Key] = kv.Value;
            }
            return context;
        }
    }
}
