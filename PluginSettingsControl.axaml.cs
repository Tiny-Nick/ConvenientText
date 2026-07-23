// ============================================================
//  PluginSettingsControl.axaml.cs
//  作用：ClassIsland 设置窗口里的“便捷文本”设置页。
//  页面结构（卡片式布局）：
//    1. 预设管理卡片：维护全局预设文本库（增/删）；
//    2. 已添加的组件卡片：列出主界面上的组件和残留数据，
//       点组件开详情设置，点 ✕ 删除残留；
//    3. 详情设置卡片：改文字/颜色/字号/时间段/悬浮按钮开关，
//       任何修改都实时自动保存；
//    4. U盘提醒卡片：ToggleSwitch 控制弹窗提醒。
// ============================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ConvenientText.Components;
using ConvenientText.Models;
using ConvenientText.Services;

using AvaloniaListBox = Avalonia.Controls.ListBox;
using AvaloniaTextBox = Avalonia.Controls.TextBox;
using AvaloniaButton = Avalonia.Controls.Button;

namespace ConvenientText
{
    [SettingsPageInfo(
        "ConvenientTextSettings",
        "便捷文本",
        SettingsPageCategory.External
    )]
    public partial class PluginSettingsControl : SettingsPageBase
    {
        private DataStorageService? _storage;
        private ObservableCollection<string> _presets = new();

        // 当前正在详情面板里编辑的组件
        private TextDataModel? _currentDetailModel;
        private readonly PropertyChangedEventHandler _detailSaveHandler;

        // 上一次保存的内容快照，用于跳过重复的保存（防止保存回声）
        private TextDataModel? _lastSavedSnapshot;

        public PluginSettingsControl()
        {
            InitializeComponent();
            _detailSaveHandler = OnDetailModelPropertyChanged;
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            _storage ??= Plugin.Storage ?? new DataStorageService();

            RefreshComponentList();
            LoadPresets();
            InitUsbToggle();

            var listBox = this.FindControl<AvaloniaListBox>("ComponentListBox");
            if (listBox != null)
                listBox.SelectionChanged += OnComponentSelected;

            // 组件加载/卸载时刷新列表
            ConvenientTextComponent.LiveModelsChanged += OnLiveModelsChanged;
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            ConvenientTextComponent.LiveModelsChanged -= OnLiveModelsChanged;
            DetachDetailModel();

            var listBox = this.FindControl<AvaloniaListBox>("ComponentListBox");
            if (listBox != null)
                listBox.SelectionChanged -= OnComponentSelected;
        }

        private void OnLiveModelsChanged(object? sender, EventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshComponentList);
        }

        // ============================================================
        //  组件列表
        // ============================================================

        /// <summary>
        /// 列表行的显示模型：序号直接按列表位置生成，
        /// 不管底层 OrderIndex 多乱，界面上永远是整齐的 1..N。
        /// </summary>
        public class ComponentRow
        {
            public int RowNumber { get; }
            public TextDataModel Model { get; }

            public ComponentRow(int rowNumber, TextDataModel model)
            {
                RowNumber = rowNumber;
                Model = model;
            }
        }

        /// <summary>
        /// 列出“主界面上的组件 + 存储里残留的组件”。
        /// </summary>
        private void RefreshComponentList()
        {
            if (_storage == null) return;

            var components = new List<TextDataModel>();
            var seen = new HashSet<string>();

            // 1) 主界面上正在运行的组件
            var live = ConvenientTextComponent.LiveModels.Values
                .Where(m => m.IsValid)
                .OrderBy(m => m.OrderIndex);
            foreach (var m in live)
            {
                components.Add(m);
                seen.Add(m.ComponentId);
            }

            // 2) 存储里有、但主界面上没加载的残留组件（可删除）
            try
            {
                var all = _storage.LoadAll();
                foreach (var m in all.Values.Where(v => v.IsValid).OrderBy(v => v.OrderIndex))
                {
                    if (seen.Add(m.ComponentId))
                        components.Add(m);
                }
            }
            catch { }

            NormalizeOrderIndexes(components);

            // 序号按显示位置生成，保证界面上永远是连续的 1..N
            var rows = components
                .Select((m, i) => new ComponentRow(i + 1, m))
                .ToList();

            var listBox = this.FindControl<AvaloniaListBox>("ComponentListBox");
            if (listBox != null)
                listBox.ItemsSource = rows;

            var emptyHint = this.FindControl<TextBlock>("EmptyComponentHint");
            if (emptyHint != null)
                emptyHint.IsVisible = rows.Count == 0;
        }

        /// <summary>
        /// 把存储里的组件序号整理成连续的 1..N（不存在就直接写入）。
        /// </summary>
        private void NormalizeOrderIndexes(List<TextDataModel> ordered)
        {
            if (_storage == null || ordered.Count == 0) return;

            bool dirty = false;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].OrderIndex != i + 1)
                {
                    ordered[i].OrderIndex = i + 1;
                    dirty = true;
                }
            }

            if (!dirty) return;

            try
            {
                var all = _storage.LoadAll();
                foreach (var m in ordered)
                {
                    // 直接整行写入（upsert），避免存储里缺这条记录时
                    // 序号被同步逻辑顶回旧值
                    all[m.ComponentId] = m.Clone();
                }
                _storage.SaveAll(all);
            }
            catch { }
        }

        private void OnComponentSelected(object? sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as AvaloniaListBox;
            if (listBox?.SelectedItem is ComponentRow row)
                ShowComponentDetail(row.Model);
        }

        private void ShowComponentDetail(TextDataModel model)
        {
            var detailPanel = this.FindControl<Border>("DetailPanel");
            var settingsPanel = this.FindControl<ConvenientText.Views.ComponentSettingsPanel>("SettingsPanel");

            if (detailPanel == null || settingsPanel == null) return;

            // 先取消上一个组件的订阅，避免重复订阅
            DetachDetailModel();

            _currentDetailModel = model;
            _lastSavedSnapshot = null; // 切换组件后重置保存快照
            _currentDetailModel.PropertyChanged += _detailSaveHandler;

            settingsPanel.SetDataModel(model);
            detailPanel.IsVisible = true;
        }

        private void DetachDetailModel()
        {
            if (_currentDetailModel != null)
            {
                _currentDetailModel.PropertyChanged -= _detailSaveHandler;
                _currentDetailModel = null;
            }
        }

        /// <summary>
        /// 详情面板里任何修改都实时写入共享存储（自动保存），
        /// 保存会广播 DataChanged，主界面组件自动同步。
        /// </summary>
        private void OnDetailModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_storage == null || sender is not TextDataModel model) return;
            if (string.IsNullOrEmpty(model.ComponentId)) return;

            // 预设列表由预设管理器单独保存，这里不处理
            if (e.PropertyName == nameof(TextDataModel.Presets)) return;

            // 内容没有实际变化就不保存（防止绑定回声造成的反复读写）
            if (_lastSavedSnapshot != null && TextDataModel.ContentEquals(_lastSavedSnapshot, model))
                return;

            try
            {
                var all = _storage.LoadAll();
                var snap = model.Clone();
                all[model.ComponentId] = snap;
                _storage.SaveAll(all);
                _lastSavedSnapshot = snap;
            }
            catch { }
        }

        /// <summary>
        /// 【新增】删除组件：主界面上正在运行的组件不能直接删
        /// （删了它也会在下一次加载时复活），提示用户去主界面移除；
        /// 未加载的残留组件直接从存储里清掉。
        /// </summary>
        private void OnDeleteComponentClick(object? sender, RoutedEventArgs e)
        {
            if (_storage == null) return;
            if ((sender as AvaloniaButton)?.Tag is not string id || string.IsNullOrEmpty(id)) return;

            if (ConvenientTextComponent.LiveModels.ContainsKey(id))
            {
                ShowHint("这个组件正在主界面上显示，不能直接在这里删除。\n\n请先在主界面进入组件编辑模式，把该组件移除；之后这里如果留下残留数据，再点 ✕ 清理即可。");
                return;
            }

            try
            {
                var all = _storage.LoadAll();
                if (all.Remove(id))
                    _storage.SaveAll(all);

                // 如果删的正是详情面板里开着的那个，收起面板
                if (_currentDetailModel?.ComponentId == id)
                {
                    DetachDetailModel();
                    var detailPanel = this.FindControl<Border>("DetailPanel");
                    if (detailPanel != null) detailPanel.IsVisible = false;
                }

                RefreshComponentList();
            }
            catch { }
        }

        private static void ShowHint(string message)
        {
            try
            {
                var dialog = new Window
                {
                    Title = "提示",
                    Width = 340,
                    Height = 185,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Spacing = 15,
                        Children =
                        {
                            new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            new AvaloniaButton
                            {
                                Content = "确定",
                                Width = 80,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                            }
                        }
                    }
                };
                var okBtn = (AvaloniaButton)((StackPanel)dialog.Content).Children[1];
                okBtn.Click += (_, _) => dialog.Close();
                dialog.Show();
            }
            catch { }
        }

        // ============================================================
        //  U盘提醒开关
        // ============================================================

        private bool _usbToggleInitialized = false;

        private void InitUsbToggle()
        {
            if (_storage == null) return;

            var usbToggle = this.FindControl<ToggleSwitch>("UsbNotificationToggle");
            if (usbToggle == null) return;

            // 设置页多次打开/切换时 OnLoaded 会重复执行，只订阅一次
            if (_usbToggleInitialized)
            {
                usbToggle.IsChecked = ReadUsbToggleState();
                return;
            }
            _usbToggleInitialized = true;

            usbToggle.IsChecked = ReadUsbToggleState();

            usbToggle.IsCheckedChanged += (_, _) =>
            {
                if (_storage == null) return;
                try
                {
                    var all = _storage.LoadAll();
                    foreach (var kv in all)
                    {
                        if (kv.Value.IsValid)
                            kv.Value.EnableUsbNotification = usbToggle.IsChecked ?? true;
                    }
                    _storage.SaveAll(all);
                }
                catch { }
            };
        }

        private bool ReadUsbToggleState()
        {
            if (_storage == null) return true;
            try
            {
                var all = _storage.LoadAll();
                var firstValid = all.Values
                    .Where(m => m.IsValid)
                    .OrderBy(m => m.OrderIndex)
                    .FirstOrDefault();
                return firstValid?.EnableUsbNotification ?? true;
            }
            catch
            {
                return true;
            }
        }

        // ============================================================
        //  预设管理（全局预设库）
        // ============================================================

        private void LoadPresets()
        {
            if (_storage == null) return;

            try
            {
                var all = _storage.LoadAll();
                var firstValid = all.Values
                    .Where(m => m.IsValid)
                    .OrderBy(m => m.OrderIndex)
                    .FirstOrDefault();
                _presets = firstValid?.Presets ?? new ObservableCollection<string>();
            }
            catch
            {
                _presets = new ObservableCollection<string>();
            }

            var presetListBox = this.FindControl<AvaloniaListBox>("PresetListBox");
            if (presetListBox != null)
                presetListBox.ItemsSource = _presets;
        }

        private void OnAddPresetClick(object? sender, RoutedEventArgs e)
        {
            var input = this.FindControl<AvaloniaTextBox>("NewPresetInput");
            if (input == null || string.IsNullOrWhiteSpace(input.Text)) return;

            var newPreset = input.Text.Trim();
            if (!_presets.Contains(newPreset))
            {
                _presets.Add(newPreset);
                SavePresets();
                input.Text = "";
            }
        }

        private void OnDeletePresetClick(object? sender, RoutedEventArgs e)
        {
            var btn = sender as AvaloniaButton;
            if (btn?.Tag is string preset)
            {
                _presets.Remove(preset);
                SavePresets();
            }
        }

        private void SavePresets()
        {
            if (_storage == null) return;

            try
            {
                var all = _storage.LoadAll();
                foreach (var kv in all)
                {
                    if (kv.Value.IsValid)
                        kv.Value.Presets = new ObservableCollection<string>(_presets);
                }
                _storage.SaveAll(all);
            }
            catch { }
        }
    }
}
