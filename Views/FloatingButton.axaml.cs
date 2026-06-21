using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using ConvenientText.Models;
using ConvenientText.Services;

namespace ConvenientText.Views;

public partial class FloatingButton : Window
{
    private readonly TextDataModel _dataModel;
    private readonly DataStorageService _storage;

    public FloatingButton(TextDataModel dataModel, DataStorageService storage)
    {
        _dataModel = dataModel;
        _storage = storage;

        // 窗口基础设置
        Width = 48;
        Height = 48;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Topmost = false;

        // 移除系统窗口装饰（标题栏、系统边框）
        SystemDecorations = SystemDecorations.None;
        // 修正：透明级别赋值为数组格式（适配0.10语法）
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };

        // 彻底透明且无边框
        Background = Brushes.Transparent;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = 0;

        // 窗口加载后调用 Win32 API 关闭系统阴影
        Loaded += OnWindowLoaded;

        // 圆形按钮（无任何阴影）
        var button = new Button
        {
            Content = "✎",
            FontSize = 22,
            Background = new SolidColorBrush(Color.FromArgb(200, 68, 68, 68)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(24),
            Width = 48,
            Height = 48,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        // 悬停效果
        button.PointerEntered += (s, e) =>
            button.Background = new SolidColorBrush(Color.FromArgb(220, 102, 102, 102));
        button.PointerExited += (s, e) =>
            button.Background = new SolidColorBrush(Color.FromArgb(200, 68, 68, 68));

        // 点击弹出编辑窗口
        button.Click += (s, e) =>
        {
            var editWindow = new EditTextWindow(_dataModel);
            editWindow.ShowDialog(this);
            editWindow.Closed += (_, _) => _storage.Save(_dataModel);
        };

        Content = button;

        // 窗口拖拽
        this.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                this.BeginMoveDrag(e);
        };
    }

    // 窗口加载完成后，关闭 Windows 系统自带的 DWM 阴影
    private void OnWindowLoaded(object sender, EventArgs e)
    {
        if (!OperatingSystem.IsWindows()) return;

        var handle = this.TryGetPlatformHandle()?.Handle;
        if (handle == null || handle.Value == IntPtr.Zero) return;

        // 关闭 DWM 非客户区渲染，彻底消除系统阴影边框
        DwmSetWindowAttribute(handle.Value, DWMWA_NCRENDERING_POLICY, 2, 4);
    }

    // Win32 DWM API 声明
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int attrValue, int attrSize);
    private const int DWMWA_NCRENDERING_POLICY = 2;
}