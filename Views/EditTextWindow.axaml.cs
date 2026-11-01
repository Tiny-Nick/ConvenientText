// ============================================================
//  EditTextWindow.axaml.cs
//  作用："编辑文本"弹窗的交互逻辑。
//  提供：文字输入、颜色选择、字号滑块、预设加载。
//  点确定后：先改传入的模型本体（界面立即刷新），再统一从
//  共享存储读出最新字典、写入本组件克隆体、整体保存。
// ============================================================

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ConvenientText.Models;
using ConvenientText.Services;

// 消除歧义别名
using AvaloniaTextBox = Avalonia.Controls.TextBox;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaColorPicker = Avalonia.Controls.ColorPicker;
using AvaloniaSlider = Avalonia.Controls.Slider;
using AvaloniaTextBlock = Avalonia.Controls.TextBlock;

namespace ConvenientText.Views
{
    public partial class EditTextWindow : Window
    {
        private readonly TextDataModel _dataModel;
        private readonly DataStorageService _storage;
        private readonly AvaloniaTextBox _inputBox;
        private readonly AvaloniaColorPicker _colorPicker;
        private readonly AvaloniaSlider _fontSizeSlider;
        private readonly AvaloniaTextBlock _sizeLabel;
        private readonly AvaloniaButton _presetButton;

        public EditTextWindow() : this(TextDataModel.CreateNew(1, Colors.Gray))
        {
        }

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="dataModel">要编辑的组件模型。
        /// 如果是主界面组件的 Settings，改动会立即反映在界面上；
        /// 确定后还会统一写入共享存储并广播同步。</param>
        public EditTextWindow(TextDataModel dataModel)
        {
            InitializeComponent();

            AcrylicTitleBarHelper.Attach(this); // 接上亚克力标题栏（拖动+关闭）
            AcrylicWindowHelper.Apply(this);    // 【1.2.2.1】按全局开关应用/取消亚克力效果

            _storage = Plugin.Storage ?? new DataStorageService();
            _dataModel = dataModel;

            _inputBox = this.FindControl<AvaloniaTextBox>("InputBox")!;
            _colorPicker = this.FindControl<AvaloniaColorPicker>("ColorPicker")!;
            _fontSizeSlider = this.FindControl<AvaloniaSlider>("FontSizeSlider")!;
            _sizeLabel = this.FindControl<AvaloniaTextBlock>("SizeLabel")!;
            _presetButton = this.FindControl<AvaloniaButton>("PresetButton")!;

            _inputBox.Text = _dataModel.DisplayText;
            _colorPicker.Color = _dataModel.TextColor;
            _fontSizeSlider.Value = _dataModel.FontSize;
            _sizeLabel.Text = ((int)_dataModel.FontSize).ToString();

            _fontSizeSlider.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                    _sizeLabel.Text = ((int)_fontSizeSlider.Value).ToString();
            };

            _presetButton.Click += OnPresetButtonClick;

            var confirmBtn = this.FindControl<AvaloniaButton>("ConfirmButton")!;
            confirmBtn.Click += OnConfirmClick;

            var cancelBtn = this.FindControl<AvaloniaButton>("CancelButton")!;
            cancelBtn.Click += OnCancelClick;
        }

        private void OnPresetButtonClick(object? sender, RoutedEventArgs e)
        {
            // 预设从共享存储的第一个有效组件读取（预设库是全局的，
            // 设置页里保存时会写入所有有效组件）
            var allData = _storage.LoadAll();
            var firstValid = allData.Values
                .Where(m => m.IsValid)
                .OrderBy(m => m.OrderIndex)
                .FirstOrDefault();
            var presets = firstValid?.Presets;

            if (presets == null || presets.Count == 0)
            {
                // 【1.2.2.1】统一走亚克力提示弹窗工厂（跟随亚克力开关）
                var dialog = AcrylicWindowHelper.CreateMessageWindow("提示", "暂无预设，请前往插件设置中添加。");
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _ = dialog.ShowDialog(this);
                return;
            }

            var presetWindow = new PresetSelectWindow(presets);
            presetWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            presetWindow.Closed += (_, _) =>
            {
                if (!string.IsNullOrEmpty(presetWindow.SelectedPreset))
                    _inputBox.Text = presetWindow.SelectedPreset;
            };

            _ = presetWindow.ShowDialog(this);
        }

        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            // 【修复】先检查这个组件还在不在存储里。
            // 如果用户在编辑窗开着的时候从设置页删了组件，这里不能复活它。
            try
            {
                var all = _storage.LoadAll();
                if (!all.TryGetValue(_dataModel.ComponentId, out var existing) || !existing.IsValid)
                {
                    // 【1.2.2.1】统一走亚克力提示弹窗工厂（跟随亚克力开关）
                    var dialog = AcrylicWindowHelper.CreateMessageWindow("提示", "该组件已被删除，保存无效。");
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    _ = dialog.ShowDialog(this);
                    this.Close();
                    return;
                }
            }
            catch { }

            // 1. 先改传入的模型本体（如果是主界面组件的 Settings，界面会立即刷新）
            _dataModel.DisplayText = _inputBox.Text ?? "";
            _dataModel.TextColor = _colorPicker.Color;
            _dataModel.FontSize = _fontSizeSlider.Value;

            // 2. 统一从磁盘读出最新字典，写入本组件的克隆体后整体保存。
            //    保存会触发 DataChanged，其它窗口/组件自动同步。
            try
            {
                var all = _storage.LoadAll();
                all[_dataModel.ComponentId] = _dataModel.Clone();
                _storage.SaveAll(all);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConvenientText] Failed to save text: {ex.Message}");
            }

            this.Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
