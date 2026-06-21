using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ConvenientText.Models;

namespace ConvenientText.Views;

public partial class EditTextWindow : Window
{
    private readonly TextDataModel _dataModel;
    private readonly TextBox _inputBox;
    private readonly ColorPicker _colorPicker;
    private readonly Slider _fontSizeSlider;
    private readonly TextBlock _sizeLabel;

    public EditTextWindow(TextDataModel dataModel)
    {
        _dataModel = dataModel;

        // ---------- 窗口设置 ----------
        Title = "文本编辑";
        Width = 520;
        Height = 145;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        // ---------- 创建界面元素 ----------
        _inputBox = new TextBox
        {
            Watermark = "输入要显示的文字...",
            AcceptsReturn = false,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Text = _dataModel.DisplayText,
            Margin = new Thickness(0, 0, 10, 0)
        };

        _colorPicker = new ColorPicker
        {
            Width = 40,
            Height = 30,
            Color = _dataModel.TextColor
        };

        _fontSizeSlider = new Slider
        {
            Minimum = 10,
            Maximum = 48,
            Value = _dataModel.FontSize,
            TickFrequency = 2,
            IsSnapToTickEnabled = true,
            Width = 80,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        _sizeLabel = new TextBlock
        {
            Text = ((int)_dataModel.FontSize).ToString(),
            Width = 25,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        // 滑块实时更新数字
        _fontSizeSlider.PropertyChanged += (s, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                _sizeLabel.Text = ((int)_fontSizeSlider.Value).ToString();
            }
        };

        // 按钮
        var confirmBtn = new Button
        {
            Content = "确定",
            Classes = { "Accent" },
            Width = 80
        };
        confirmBtn.Click += OnConfirmClick;

        var cancelBtn = new Button
        {
            Content = "取消",
            Width = 80
        };
        cancelBtn.Click += OnCancelClick;

        // ---------- 布局 ----------
        // 第一行：输入框 + 颜色 + 字号
        var row1 = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 15) };

        // 字号区域（最右边）
        var sizePanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        sizePanel.Children.Add(new TextBlock { Text = "字号", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        sizePanel.Children.Add(_fontSizeSlider);
        sizePanel.Children.Add(_sizeLabel);
        DockPanel.SetDock(sizePanel, Dock.Right);
        row1.Children.Add(sizePanel);

        // 颜色区域（右边第二）
        var colorPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 5,
            Margin = new Thickness(10, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        colorPanel.Children.Add(new TextBlock { Text = "颜色", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        colorPanel.Children.Add(_colorPicker);
        DockPanel.SetDock(colorPanel, Dock.Right);
        row1.Children.Add(colorPanel);

        // 输入框（填充剩余空间）
        row1.Children.Add(_inputBox);

        // 第二行：按钮
        var row2 = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        row2.Children.Add(cancelBtn);
        row2.Children.Add(confirmBtn);

        // 总布局
        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Grid.SetRow(row1, 0);
        Grid.SetRow(row2, 1);
        grid.Children.Add(row1);
        grid.Children.Add(row2);

        Content = grid;
    }

    private void OnConfirmClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _dataModel.DisplayText = _inputBox.Text ?? "";
        _dataModel.TextColor = _colorPicker.Color;
        _dataModel.FontSize = _fontSizeSlider.Value;
        Close(true);
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}