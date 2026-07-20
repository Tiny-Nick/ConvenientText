using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using ConvenientText.Models;
using ConvenientText.Services;
using System;
using System.Runtime.InteropServices;

// 消除类型歧义
using AvaloniaPoint = Avalonia.Point;
using AvaloniaBrushes = Avalonia.Media.Brushes;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaColor = Avalonia.Media.Color;
using AvaloniaCursor = Avalonia.Input.Cursor;

namespace ConvenientText.Views
{
    public partial class FloatingButton : Window
    {
        private readonly TextDataModel _dataModel;
        private readonly DataStorageService _storage;
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _isLoaded = false;

        // 拖动相关（使用屏幕像素坐标）
        private bool _isPointerDown = false;
        private bool _isDragging = false;
        private PixelPoint _windowPosOnDown;      // 按下时的窗口位置（屏幕像素）
        private PixelPoint _mouseScreenOnDown;    // 按下时鼠标的屏幕像素坐标
        private const double DragThreshold = 10;  // 像素

        public FloatingButton(TextDataModel dataModel, DataStorageService storage)
        {
            _dataModel = dataModel;
            _storage = storage;

            Width = 56;
            Height = 56;
            CanResize = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Topmost = false;
            Title = "";

            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = AvaloniaBrushes.Transparent;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 0;

            Position = new PixelPoint((int)_dataModel.FloatingX, (int)_dataModel.FloatingY);

            this.Loaded += OnLoaded;
            this.Deactivated += OnDeactivated;

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

            // 按钮点击打开编辑窗口
            button.Click += (s, e) =>
            {
                var editWindow = new EditTextWindow(_dataModel);
                editWindow.ShowDialog(this);
                editWindow.Closed += (_, _) => _storage.Save(_dataModel);
            };

            // 窗口级别指针事件（用于拖动）
            this.PointerPressed += OnPointerPressed;
            this.PointerMoved += OnPointerMoved;
            this.PointerReleased += OnPointerReleased;
            this.Closed += (_, _) => _storage.Save(_dataModel);
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
                if (!_isDragging)
                {
                    _isDragging = true;
                }

                // 新位置 = 初始窗口位置 + 鼠标位移
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
                _dataModel.FloatingX = pos.X;
                _dataModel.FloatingY = pos.Y;
                _storage.Save(_dataModel);

                if (_hwnd != IntPtr.Zero)
                {
                    SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
                _isDragging = false;
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

            DwmSetWindowAttribute(_hwnd, DWMWA_NCRENDERING_POLICY, 2, 4);

            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

            SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private void OnDeactivated(object? sender, EventArgs e)
        {
            if (_hwnd != IntPtr.Zero)
            {
                SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
        }

        // === Win32 互操作（保持不变） ===
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