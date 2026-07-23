// ============================================================
//  ComponentListWindow.axaml.cs
//  作用：点击悬浮按钮后弹出的“选择要编辑的组件”窗口。
//  列出主界面上的组件（没有则退回读存储），点击某项即打开
//  对应的“编辑文本”窗口；数据变化时列表自动刷新。
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ConvenientText.Components;
using ConvenientText.Models;
using ConvenientText.Services;

using AvaloniaListBox = Avalonia.Controls.ListBox;

namespace ConvenientText.Views
{
    public partial class ComponentListWindow : Window
    {
        private readonly DataStorageService _storage;
        private List<TextDataModel> _components = new();

        public ComponentListWindow()
        {
            InitializeComponent();

            AcrylicTitleBarHelper.Attach(this);

            _storage = Plugin.Storage ?? new DataStorageService();
            LoadComponents();

            var listBox = this.FindControl<AvaloniaListBox>("ComponentListBox");
            if (listBox != null)
            {
                listBox.ItemsSource = _components;
                listBox.DoubleTapped += OnItemDoubleTapped;
                listBox.SelectionChanged += OnSelectionChanged;
            }

            // 数据变化时刷新列表（比如刚在编辑窗口里改完文字）
            _storage.DataChanged += OnStorageDataChanged;
        }

        /// <summary>
        /// 【修复】优先列出“真正加载在主界面上的组件”，
        /// 避免旧版把已删除组件的残留数据也列出来。
        /// </summary>
        private void LoadComponents()
        {
            var live = ConvenientTextComponent.LiveModels;

            if (live.Count > 0)
            {
                _components = live.Values
                    .Where(m => m.IsValid)
                    .OrderBy(m => m.OrderIndex)
                    .ToList();
            }
            else
            {
                var all = _storage.LoadAll();
                _components = all.Values
                    .Where(m => m.IsValid)
                    .OrderBy(m => m.OrderIndex)
                    .ToList();
            }
        }

        private void OnStorageDataChanged(object? sender, EventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    LoadComponents();
                    var listBox = this.FindControl<AvaloniaListBox>("ComponentListBox");
                    if (listBox != null)
                    {
                        listBox.ItemsSource = null;
                        listBox.ItemsSource = _components;
                    }
                }
                catch { }
            });
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as AvaloniaListBox;
            if (listBox?.SelectedItem is TextDataModel selected)
            {
                // 打开编辑后清除选中，否则刷新列表时会反复弹窗
                listBox.SelectedItem = null;
                OpenEditWindow(selected);
            }
        }

        private void OnItemDoubleTapped(object? sender, RoutedEventArgs e)
        {
            var listBox = sender as AvaloniaListBox;
            if (listBox?.SelectedItem is TextDataModel selected)
            {
                OpenEditWindow(selected);
            }
        }

        private void OpenEditWindow(TextDataModel model)
        {
            var editWindow = new EditTextWindow(model);
            editWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            editWindow.Show(this);
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _storage.DataChanged -= OnStorageDataChanged;
            var listBox = this.FindControl<AvaloniaListBox>("ComponentListBox");
            if (listBox != null)
            {
                listBox.SelectionChanged -= OnSelectionChanged;
                listBox.DoubleTapped -= OnItemDoubleTapped;
            }
        }
    }
}
