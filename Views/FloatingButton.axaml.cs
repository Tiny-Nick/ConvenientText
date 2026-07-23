// ============================================================
//  FloatingButton.axaml.cs
//  作用：桌面上的悬浮编辑按钮（一个无边框小圆钮窗口）。
//  特性：
//    · 置底显示（通过 Win32 SetWindowPos 放到窗口层最底部）；
//    · 可拖动，松手后把新位置保存到共享存储；
//    · 点击打开“选择组件”窗口；
//    · 是否显示由 FloatingWindowHostedService（Plugin.cs 里）
//      根据开关、时间段、组件是否加载来统一控制。
//  本文件也是代码建界面，没有对应的 .axaml 文件。
// ============================================================

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using ConvenientText.Models;
using ConvenientText.Services;

// 消除歧义别名
using AvaloniaBrushes = Avalonia.Media.Brushes;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaColor = Avalonia.Media.Color;
using AvaloniaCursor = Avalonia.Input.Cursor;

namespace ConvenientText.Views
{
    /// <summary>
    /// 桌面悬浮编辑按钮。一个 56x56 的无边框透明小窗口，
    /// 里面放一个圆形 ✎ 按钮；支持拖动换位，点击打开组件列表。
    /// </summary>
    public partial class FloatingButton : Window
    {
        /// <summary>当前绑定的组件数据（决定按钮初始位置；拖动后坐标也写回它）</summary>
        private TextDataModel _dataModel;

        /// <summary>共享数据存储，拖动结束时保存新位置</summary>
        private readonly DataStorageService _storage;

        /// <summary>窗口句柄（Win32 置底操作用，仅 Windows 有效）</summary>
        private IntPtr _hwnd = IntPtr.Zero;

        /// <summary>防止 Loaded 事件重复初始化</summary>
        private bool _isLoaded = false;

        // ----- 拖动状态机 -----
        private bool _isPointerDown = false;    // 左键是否按着
        private bool _isDragging = false;       // 是否已经进入拖动（超过阈值）
        private PixelPoint _windowPosOnDown;    // 按下瞬间的窗口位置
        private PixelPoint _mouseScreenOnDown;  // 按下瞬间的鼠标屏幕坐标

        /// <summary>拖动阈值（像素）：按下后移动超过它才算拖动，否则算点击</summary>
        private const double DragThreshold = 10;

        public FloatingButton(TextDataModel dataModel, DataStorageService storage)
        {
            _dataModel = dataModel;
            _storage = storage;

            // ----- 窗口基本形态：小而安静的桌面小部件 -----
            Width = 56;
            Height = 56;
            CanResize = false;                 // 不可调大小
            ShowInTaskbar = false;             // 不占任务栏
            WindowStartupLocation = WindowStartupLocation.Manual; // 位置由我们自己定
            Topmost = false;                   // 不置顶（要当桌面“贴纸”）
            Title = "";

            // ----- 无边框 + 全透明：只露出中间的圆形按钮 -----
            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = AvaloniaBrushes.Transparent;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 0;

            // 初始位置取上次保存的坐标
            Position = new PixelPoint((int)_dataModel.FloatingX, (int)_dataModel.FloatingY);

            this.Loaded += OnLoaded!;
            this.Deactivated += OnDeactivated!;

            // ----- 圆形 ✎ 按钮本体 -----
            var button = new AvaloniaButton
            {
                Content = "✎",
                FontSize = 18,
                Background = new SolidColorBrush(AvaloniaColor.FromArgb(220, 68, 68, 68)),
                Foreground = AvaloniaBrushes.White,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(20),
                Width = 40,
                Height = 40,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Cursor = new AvaloniaCursor(StandardCursorType.Hand)
            };

            var grid = new Grid();
            grid.Children.Add(button);
            button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            button.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            Content = grid;

            button.Click += OnButtonClick;

            this.PointerPressed += OnPointerPressed!;
            this.PointerMoved += OnPointerMoved!;
            this.PointerReleased += OnPointerReleased!;
        }

        public void UpdateDataModel(TextDataModel newModel)
        {
            bool sameComponent = _dataModel.ComponentId == newModel.ComponentId;
            _dataModel = newModel;
            // 只有切换到另一个组件时才跳转位置；同一组件的数据刷新不动窗口
            if (!sameComponent)
                Position = new PixelPoint((int)_dataModel.FloatingX, (int)_dataModel.FloatingY);
        }

        private void OnButtonClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var listWindow = new ComponentListWindow();
                listWindow.Show();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConvenientText] Failed to open ComponentListWindow: {ex.Message}");
            }
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;

            if (!OperatingSystem.IsWindows()) return;
            var handle = this.TryGetPlatformHandle()?.Handle;
            if (handle == null || handle.Value == IntPtr.Zero) return;
            _hwnd = handle.Value;

            try
            {
                DwmSetWindowAttribute(_hwnd, DWMWA_NCRENDERING_POLICY, 2, 4);

                int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
                SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

                SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            catch { }
        }

        private void OnDeactivated(object? sender, EventArgs e)
        {
            if (_hwnd != IntPtr.Zero)
            {
                try
                {
                    SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
                catch { }
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPointerDown = true;
                _isDragging = false;
                _windowPosOnDown = this.Position;
                _mouseScreenOnDown = this.PointToScreen(e.GetPosition(this));
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPointerDown) return;

            var mouseScreenCurrent = this.PointToScreen(e.GetPosition(this));
            var delta = mouseScreenCurrent - _mouseScreenOnDown;

            if (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold)
            {
                if (!_isDragging) _isDragging = true;

                var newX = _windowPosOnDown.X + delta.X;
                var newY = _windowPosOnDown.Y + delta.Y;
                this.Position = new PixelPoint(newX, newY);
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isPointerDown) return;
            _isPointerDown = false;

            if (_isDragging)
            {
                var pos = this.Position;

                // 【修复】旧版先把新坐标写进内存对象，却又从磁盘重新读了一份
                // 旧数据再保存，导致坐标永远存不上。现在先读出字典、更新
                // 对应组件的坐标、再整体保存。
                try
                {
                    var all = _storage.LoadAll();
                    if (all.TryGetValue(_dataModel.ComponentId, out var stored))
                    {
                        stored.FloatingX = pos.X;
                        stored.FloatingY = pos.Y;
                    }
                    else
                    {
                        _dataModel.FloatingX = pos.X;
                        _dataModel.FloatingY = pos.Y;
                        all[_dataModel.ComponentId] = _dataModel.Clone();
                    }
                    _storage.SaveAll(all);
                }
                catch { }

                if (_hwnd != IntPtr.Zero)
                {
                    try
                    {
                        SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    }
                    catch { }
                }
                _isDragging = false;
            }
        }

        // ============================================================
        //  Win32 P/Invoke
        // ============================================================
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int attrValue, int attrSize);
        private const int DWMWA_NCRENDERING_POLICY = 2;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
    }
}
