using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ConvenientText.Models;
using System;

namespace ConvenientText.Views
{
    public partial class EditTextWindow : Window
    {
        private readonly TextDataModel _dataModel;
        private readonly Avalonia.Controls.TextBox _inputBox;
        private readonly Avalonia.Controls.ColorPicker _colorPicker;
        private readonly Avalonia.Controls.Slider _fontSizeSlider;
        private readonly Avalonia.Controls.TextBlock _sizeLabel;

        public EditTextWindow(TextDataModel dataModel)
        {
            _dataModel = dataModel;

            Title = "文本编辑";
            Width = 520;
            Height = 130;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;

            _inputBox = new Avalonia.Controls.TextBox
            {
                Watermark = "输入要显示的文字...",
                AcceptsReturn = false,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Text = _dataModel.DisplayText,
                Margin = new Thickness(0, 0, 10, 0)
            };

            _colorPicker = new Avalonia.Controls.ColorPicker
            {
                Width = 40,
                Height = 30,
                Color = _dataModel.TextColor
            };

            _fontSizeSlider = new Avalonia.Controls.Slider
            {
                Minimum = 10,
                Maximum = 48,
                Value = _dataModel.FontSize,
                TickFrequency = 2,
                IsSnapToTickEnabled = true,
                Width = 80,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            _sizeLabel = new Avalonia.Controls.TextBlock
            {
                Text = ((int)_dataModel.FontSize).ToString(),
                Width = 25,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            _fontSizeSlider.PropertyChanged += (s, e) =>
            {
                if (e.Property == Slider.ValueProperty)
                    _sizeLabel.Text = ((int)_fontSizeSlider.Value).ToString();
            };

            var confirmBtn = new Avalonia.Controls.Button
            {
                Content = "确定",
                Classes = { "Accent" },
                Width = 80
            };
            confirmBtn.Click += OnConfirmClick;

            var cancelBtn = new Avalonia.Controls.Button
            {
                Content = "取消",
                Width = 80
            };
            cancelBtn.Click += OnCancelClick;

            var row1 = new Avalonia.Controls.DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 15) };

            var sizePanel = new Avalonia.Controls.StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            sizePanel.Children.Add(new Avalonia.Controls.TextBlock { Text = "字号", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            sizePanel.Children.Add(_fontSizeSlider);
            sizePanel.Children.Add(_sizeLabel);
            Avalonia.Controls.DockPanel.SetDock(sizePanel, Dock.Right);
            row1.Children.Add(sizePanel);

            var colorPanel = new Avalonia.Controls.StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 5,
                Margin = new Thickness(10, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            colorPanel.Children.Add(new Avalonia.Controls.TextBlock { Text = "颜色", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            colorPanel.Children.Add(_colorPicker);
            Avalonia.Controls.DockPanel.SetDock(colorPanel, Dock.Right);
            row1.Children.Add(colorPanel);

            row1.Children.Add(_inputBox);

            var row2 = new Avalonia.Controls.StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };
            row2.Children.Add(cancelBtn);
            row2.Children.Add(confirmBtn);

            var grid = new Avalonia.Controls.Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new Avalonia.Controls.RowDefinition(Avalonia.Controls.GridLength.Auto));
            grid.RowDefinitions.Add(new Avalonia.Controls.RowDefinition(Avalonia.Controls.GridLength.Auto));
            Avalonia.Controls.Grid.SetRow(row1, 0);
            Avalonia.Controls.Grid.SetRow(row2, 1);
            grid.Children.Add(row1);
            grid.Children.Add(row2);

            Content = grid;
        }

        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            _dataModel.DisplayText = _inputBox.Text ?? "";
            _dataModel.TextColor = _colorPicker.Color;
            _dataModel.FontSize = _fontSizeSlider.Value;
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}