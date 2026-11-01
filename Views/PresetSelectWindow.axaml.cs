// ============================================================
//  PresetSelectWindow.axaml.cs
//  作用："选择预设文本"弹窗的交互逻辑。
//
//  这个窗口是模态的（ShowDialog），用户从分组预设列表中选择一项后，
//  结果通过 SelectedPresetText 属性传回给"编辑文本"窗口。
//
//  交互方式：
//    - 单击选中：立即返回选中值并关闭窗口
//    - 点取消：返回 null（不修改输入框）
//
//  【1.2.2.0】预设列表支持按分类分组显示
// ============================================================

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ConvenientText.Models;

namespace ConvenientText.Views
{
    /// <summary>
    /// 预设文本选择窗口。按分类分组显示，支持点击选择。
    /// </summary>
    public partial class PresetSelectWindow : Window
    {
        /// <summary>
        /// 用户选中的预设文本。null 表示用户取消了选择。
        /// </summary>
        public string? SelectedPreset { get; private set; }

        public PresetSelectWindow() : this(new ObservableCollection<PresetItem>())
        {
        }

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="presets">预设条目集合（来自全局预设库）</param>
        public PresetSelectWindow(ObservableCollection<PresetItem> presets)
        {
            InitializeComponent();

            AcrylicTitleBarHelper.Attach(this);
            AcrylicWindowHelper.Apply(this);    // 【1.2.2.1】按全局开关应用/取消亚克力效果

            // 控件初始化阶段 FindResource 可能返回 UnsetValue，全部用 try-catch 兜底
            IBrush hintBrush;
            try { hintBrush = this.FindResource("SystemControlDisabledChromeDisabledBrush") as IBrush ?? Brush.Parse("#99FFFFFF"); }
            catch { hintBrush = Brush.Parse("#99FFFFFF"); }

            IBrush hoverBrush;
            try { hoverBrush = this.FindResource("SystemControlBackgroundChromeMediumBrush") as IBrush ?? Brush.Parse("#20FFFFFF"); }
            catch { hoverBrush = Brush.Parse("#20FFFFFF"); }

            var panel = this.FindControl<StackPanel>("PresetGroupPanel");
            if (panel == null) return;

            if (presets == null || presets.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "暂无预设，请到插件设置中添加",
                    Foreground = hintBrush,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    FontSize = 14,
                    Margin = new Avalonia.Thickness(0, 20)
                });
                return;
            }

            // 按分类分组显示
            var groups = presets
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? "未分类" : p.Category.Trim())
                .OrderBy(g => g.Key == "未分类" ? "ZZZ" : g.Key);

            foreach (var group in groups)
            {
                // 分类标题
                panel.Children.Add(new TextBlock
                {
                    Text = $"▸ {group.Key}",
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = hintBrush,
                    Margin = new Avalonia.Thickness(0, 8, 0, 4)
                });

                // 该分类下的每个预设条目
                foreach (var item in group.OrderBy(p => p.Name))
                {
                    var row = new Border
                    {
                        CornerRadius = new Avalonia.CornerRadius(6),
                        Padding = new Avalonia.Thickness(10, 8),
                        Margin = new Avalonia.Thickness(8, 2),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Background = Brushes.Transparent,
                        Tag = item.Text
                    };

                    row.PointerEntered += (_, _) => row.Background = hoverBrush;
                    row.PointerExited += (_, _) => row.Background = Brushes.Transparent;

                    var contentGrid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto")
                    };

                    var textBlock = new TextBlock
                    {
                        Text = item.Name,
                        FontSize = 14,
                        TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };
                    Grid.SetColumn(textBlock, 0);
                    contentGrid.Children.Add(textBlock);

                    var arrow = new TextBlock
                    {
                        Text = "▶",
                        FontSize = 12,
                        Opacity = 0.3,
                        Margin = new Avalonia.Thickness(10, 0, 0, 0),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    };
                    Grid.SetColumn(arrow, 1);
                    contentGrid.Children.Add(arrow);

                    row.Child = contentGrid;

                    row.PointerPressed += (_, ev) =>
                    {
                        if (ev.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                        {
                            SelectedPreset = item.Text;
                            this.Close();
                        }
                    };

                    panel.Children.Add(row);
                }
            }
        }

        /// <summary>
        /// 点击取消按钮：不设置 SelectedPreset（保持 null），关闭窗口。
        /// </summary>
        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            SelectedPreset = null;
            this.Close();
        }
    }
}
