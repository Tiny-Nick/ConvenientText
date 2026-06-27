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

namespace ConvenientText.Views
{
    public partial class FloatingButton : Window
    {
        private readonly TextDataModel _dataModel;
        private readonly DataStorageService _storage;
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _isLoaded = false;

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
            Background = Avalonia.Media.Brushes.Transparent;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 0;

            Position = new PixelPoint((int)_dataModel.FloatingX, (int)_dataModel.FloatingY);

            this.Loaded += OnLoaded;
            this.Deactivated += OnDeactivated;

            // 关键：使用完全限定名 Avalonia.Controls.Button
            var button = new Avalonia.Controls.Button
            {
                Content = "✎",
                FontSize = 18,
                Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(220, 68, 68, 68)),
                Foreground = Avalonia.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(20),
                Width = 40,
                Height = 40,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Cursor = new Avalonia.Input.Cursor(StandardCursorType.Hand)
            };

            var grid = new Grid();
            grid.Children.Add(button);
            button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            button.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            Content = grid;

            button.PointerEntered += (s, e) =>
                button.Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(235, 102, 102, 102));
            button.PointerExited += (s, e) =>
                button.Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(220, 68, 68, 68));

            button.Click += (s, e) =>
            {
                var editWindow = new EditTextWindow(_dataModel);
                editWindow.ShowDialog(this);
                editWindow.Closed += (_, _) => _storage.Save(_dataModel);
            };

            this.PointerPressed += OnPointerPressed;
            this.PointerReleased += OnPointerReleased;
            this.Closed += (_, _) => _storage.Save(_dataModel);
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

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            var pos = this.Position;
            _dataModel.FloatingX = pos.X;
            _dataModel.FloatingY = pos.Y;
            _storage.Save(_dataModel);
        }

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