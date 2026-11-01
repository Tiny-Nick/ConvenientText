// ============================================================
//  PluginSettingsControl.axaml.cs
//  作用：ClassIsland 设置窗口里的“便捷文本”设置页。
//  页面结构（卡片式布局）：
//    1. 预设管理卡片：维护全局预设文本库（增/删）；
//    2. 已添加的组件卡片：列出主界面上的组件和残留数据，
//       点组件开详情设置，点 ✕ 删除残留；
//    3. 详情设置卡片：改文字/颜色/字号/时间段，任何修改实时自动保存；
//    4. 悬浮按钮卡片：全局开关，控制桌面悬浮按钮的显隐；
//    5. U盘提醒卡片：ToggleSwitch 控制弹窗提醒。
// ============================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ConvenientText.Components;
using ConvenientText.Models;
using ConvenientText.Services;

using AvaloniaListBox = Avalonia.Controls.ListBox;
using AvaloniaTextBox = Avalonia.Controls.TextBox;
using AvaloniaButton = Avalonia.Controls.Button;
using ConvenientText.Views;

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
        private ObservableCollection<PresetItem> _presets = new();

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
            InitFloatingButtonToggle();
            InitAcrylicToggle();

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

            // 1) 主界面上正在运行的组件（线程安全快照）
            var live = ConvenientTextComponent.GetLiveModelsSnapshot();
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
                // 【1.2.2.1】统一走亚克力提示弹窗工厂（跟随亚克力开关）
                AcrylicWindowHelper.CreateMessageWindow("提示", message).Show();
            }
            catch { }
        }

        // ============================================================
        //  悬浮按钮全局开关
        // ============================================================

        private void InitFloatingButtonToggle()
        {
            if (_storage == null) return;

            var toggle = this.FindControl<ToggleSwitch>("FloatingButtonToggle");
            if (toggle == null) return;

            if (_floatingButtonToggleInitialized)
            {
                toggle.IsChecked = _storage.GlobalFloatingButtonEnabled;
                return;
            }
            _floatingButtonToggleInitialized = true;

            toggle.IsChecked = _storage.GlobalFloatingButtonEnabled;

            toggle.IsCheckedChanged += (_, _) =>
            {
                if (_storage == null) return;
                _storage.GlobalFloatingButtonEnabled = toggle.IsChecked ?? true;
            };
        }

        // ============================================================
        //  U盘提醒开关
        // ============================================================

        private bool _usbToggleInitialized = false;
        private bool _floatingButtonToggleInitialized = false;
        private bool _acrylicToggleInitialized = false;

        /// <summary>
        /// 【1.2.2.1】窗口亚克力效果全局开关。
        /// 关闭后弹窗使用不透明背景（新打开的窗口生效）。
        /// </summary>
        private void InitAcrylicToggle()
        {
            if (_storage == null) return;

            var toggle = this.FindControl<ToggleSwitch>("AcrylicToggle");
            if (toggle == null) return;

            // 设置页多次打开/切换时 OnLoaded 会重复执行，只订阅一次
            if (_acrylicToggleInitialized)
            {
                toggle.IsChecked = _storage.GlobalUseAcrylic;
                return;
            }
            _acrylicToggleInitialized = true;

            toggle.IsChecked = _storage.GlobalUseAcrylic;

            toggle.IsCheckedChanged += (_, _) =>
            {
                if (_storage == null) return;
                _storage.GlobalUseAcrylic = toggle.IsChecked ?? true;
            };
        }

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

                // 【修复】必须深拷贝：_presets 必须是独立集合，绝不能直接引用
                // 存储模型的 Presets。否则 SavePresets → SaveAll → DataChanged
                // → CopyFrom 链路会替换存储模型上的集合，导致 _presets 指向
                // 过期对象，RebuildPresetGroupUI 读到旧数据。
                var source = firstValid?.Presets ?? new ObservableCollection<PresetItem>();
                _presets = new ObservableCollection<PresetItem>(
                    source.Select(p => new PresetItem { Name = p.Name, Text = p.Text, Category = p.Category }));
            }
            catch
            {
                _presets = new ObservableCollection<PresetItem>();
            }

            RebuildPresetGroupUI();
        }

        /// <summary>
        /// 按分类重建预设分组显示界面。
        /// 每组一个圆角卡片，内含该类目下的所有预设条目。
        /// </summary>
        private void RebuildPresetGroupUI()
        {
            var panel = this.FindControl<StackPanel>("PresetGroupPanel");
            if (panel == null) return;
            panel.Children.Clear();

            // 控件初始化阶段 FindResource 可能返回 UnsetValue，用 try-catch 兜底
            IBrush hintBrush;
            try { hintBrush = this.FindResource("SystemControlDisabledChromeDisabledBrush") as IBrush ?? Brush.Parse("#99FFFFFF"); }
            catch { hintBrush = Brush.Parse("#99FFFFFF"); }

            if (_presets.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "暂无预设，使用下方输入框添加",
                    FontSize = 13,
                    Foreground = hintBrush,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8)
                });
                return;
            }

            // 按分类分组
            var groups = _presets
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? "未分类" : p.Category.Trim())
                .OrderBy(g => g.Key == "未分类" ? "ZZZ" : g.Key); // 未分类排最后

            foreach (var group in groups)
            {
                // 分类标题
                panel.Children.Add(new TextBlock
                {
                    Text = $"▸ {group.Key}",
                    FontSize = 12,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    Foreground = hintBrush,
                    Margin = new Thickness(0, 4, 0, 2)
                });

                // 每组内的条目
                foreach (var item in group.OrderBy(p => p.Name))
                {
                    var row = new Grid
                    {
                        Margin = new Thickness(8, 2),
                        ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto")
                    };

                    var nameText = new TextBlock
                    {
                        Text = item.Name,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                        FontSize = 13
                    };
                    Grid.SetColumn(nameText, 0);
                    row.Children.Add(nameText);

                    // 【新增】编辑按钮
                    var editBtn = new AvaloniaButton
                    {
                        Content = "✎",
                        FontSize = 12,
                        Width = 26,
                        Height = 26,
                        Padding = new Thickness(0),
                        CornerRadius = new CornerRadius(4),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = Brush.Parse("#CCFFFFFF"),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        Tag = item
                    };
                    editBtn.Click += OnEditPresetClick;
                    // 悬停变红
                    editBtn.PointerEntered += (_, _) => { editBtn.Background = Brush.Parse("#E81123"); editBtn.Foreground = Brushes.White; };
                    editBtn.PointerExited += (_, _) => { editBtn.Background = Brushes.Transparent; editBtn.Foreground = Brush.Parse("#CCFFFFFF"); };
                    Grid.SetColumn(editBtn, 1);
                    row.Children.Add(editBtn);

                    // 删除按钮
                    var delBtn = new AvaloniaButton
                    {
                        Content = "✕",
                        FontSize = 13,
                        Width = 26,
                        Height = 26,
                        Padding = new Thickness(0),
                        CornerRadius = new CornerRadius(4),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = Brush.Parse("#CCFFFFFF"),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        Tag = item,
                        Margin = new Thickness(2, 0, 0, 0)
                    };
                    delBtn.Click += OnDeletePresetClick;
                    delBtn.PointerEntered += (_, _) => { delBtn.Background = Brush.Parse("#E81123"); delBtn.Foreground = Brushes.White; };
                    delBtn.PointerExited += (_, _) => { delBtn.Background = Brushes.Transparent; delBtn.Foreground = Brush.Parse("#CCFFFFFF"); };
                    Grid.SetColumn(delBtn, 2);
                    row.Children.Add(delBtn);

                    panel.Children.Add(row);
                }
            }
        }

        /// <summary>
        /// 【新增】编辑预设：把内容放进输入框，然后从列表中删除。
        /// </summary>
        private void OnEditPresetClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not AvaloniaButton btn || btn.Tag is not PresetItem item) return;

            var nameInput = this.FindControl<AvaloniaTextBox>("NewPresetInput");
            var catInput = this.FindControl<AvaloniaTextBox>("NewPresetCategoryInput");
            if (nameInput != null) nameInput.Text = item.Name;
            if (catInput != null) catInput.Text = item.Category;
            _presets.Remove(item);
            SavePresets();
        }

        private void OnAddPresetClick(object? sender, RoutedEventArgs e)
        {
            var input = this.FindControl<AvaloniaTextBox>("NewPresetInput");
            if (input == null || string.IsNullOrWhiteSpace(input.Text)) return;

            var catInput = this.FindControl<AvaloniaTextBox>("NewPresetCategoryInput");
            var category = catInput?.Text?.Trim() ?? "";

            var text = input.Text.Trim();
            var newPreset = new PresetItem { Name = text, Text = text, Category = category };

            // 避免完全重复的预设
            if (!_presets.Any(p => p.Text == newPreset.Text && p.Category == newPreset.Category))
            {
                _presets.Add(newPreset);
                SavePresets();
                input.Text = "";
                if (catInput != null) catInput.Text = "";
            }
        }

        private void OnDeletePresetClick(object? sender, RoutedEventArgs e)
        {
            if (sender is AvaloniaButton btn && btn.Tag is PresetItem preset)
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
                        kv.Value.Presets = new ObservableCollection<PresetItem>(
                            _presets.Select(p => new PresetItem { Name = p.Name, Text = p.Text, Category = p.Category }));
                }
                _storage.SaveAll(all);
            }
            catch { }

            // 刷新分组显示
            RebuildPresetGroupUI();
        }
    }
}
