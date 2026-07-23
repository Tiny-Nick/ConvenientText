// ============================================================
//  PresetSelectWindow.axaml.cs
//  作用：“选择预设”弹窗。展示预设文本列表，点选后通过
//  SelectedPreset 属性把结果返回给“编辑文本”窗口。
// ============================================================

using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

using AvaloniaListBox = Avalonia.Controls.ListBox;

namespace ConvenientText.Views
{
    public partial class PresetSelectWindow : Window
    {
        public string? SelectedPreset { get; private set; }

        public PresetSelectWindow(ObservableCollection<string> presets)
        {
            InitializeComponent();

            AcrylicTitleBarHelper.Attach(this);

            var listBox = this.FindControl<AvaloniaListBox>("PresetListBox");
            var emptyHint = this.FindControl<TextBlock>("EmptyHint");

            if (presets == null || presets.Count == 0)
            {
                if (emptyHint != null) emptyHint.IsVisible = true;
                if (listBox != null) listBox.IsVisible = false;
                return;
            }

            if (listBox != null)
            {
                listBox.ItemsSource = presets;
                listBox.DoubleTapped += OnItemDoubleTapped;
                listBox.SelectionChanged += OnSelectionChanged;
            }
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as AvaloniaListBox;
            if (listBox?.SelectedItem is string selected)
            {
                SelectedPreset = selected;
                this.Close();
            }
        }

        private void OnItemDoubleTapped(object? sender, RoutedEventArgs e)
        {
            var listBox = sender as AvaloniaListBox;
            if (listBox?.SelectedItem is string selected)
            {
                SelectedPreset = selected;
                this.Close();
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            SelectedPreset = null;
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            var listBox = this.FindControl<AvaloniaListBox>("PresetListBox");
            if (listBox != null)
            {
                listBox.SelectionChanged -= OnSelectionChanged;
                listBox.DoubleTapped -= OnItemDoubleTapped;
            }
        }
    }
}
