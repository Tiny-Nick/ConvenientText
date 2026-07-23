// ============================================================
//  AcrylicTitleBarHelper.cs
//  作用：无边框亚克力弹窗的标题栏助手。
//  弹窗用了 ExtendClientAreaToDecorationsHint 后系统标题栏
//  就没有了，需要在 XAML 里自绘标题栏（一个名为 TitleBar
//  的区域 + 一个名为 TitleBarCloseButton 的按钮），
//  本助手负责给它们接上“拖动窗口”和“关闭”功能。
// ============================================================

using Avalonia.Controls;
using Avalonia.Input;

namespace ConvenientText.Views
{
    /// <summary>
    /// 给使用亚克力无边框样式的窗口接上自定义标题栏的功能：
    /// 按住标题栏拖动窗口 + 关闭按钮。
    /// </summary>
    public static class AcrylicTitleBarHelper
    {
        /// <summary>
        /// 在窗口构造时调用。要求 XAML 里有名为 "TitleBar" 的容器
        /// 和名为 "TitleBarCloseButton" 的按钮。
        /// </summary>
        public static void Attach(Window window)
        {
            var titleBar = window.FindControl<Grid>("TitleBar");
            if (titleBar != null)
            {
                titleBar.PointerPressed += (s, e) =>
                {
                    if (e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
                        window.BeginMoveDrag(e);
                };
            }

            var closeButton = window.FindControl<Button>("TitleBarCloseButton");
            if (closeButton != null)
            {
                closeButton.Click += (_, _) => window.Close();
            }
        }
    }
}
