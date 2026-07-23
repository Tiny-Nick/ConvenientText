// ============================================================
//  ConvenientTextSettingsControl.axaml.cs
//  作用：“便捷文本”组件在 ClassIsland 组件设置里的面板。
//  组件的设置统一放在插件设置页里，这里只显示一句提示。
// ============================================================

using Avalonia.Controls;

namespace ConvenientText.Components
{
    // 这个类是“便捷文本”组件在 ClassIsland 组件设置里的设置面板。
    // 目前该组件不需要额外设置，所以这里只是加载对应的 XAML 界面。
    public partial class ConvenientTextSettingsControl : Avalonia.Controls.UserControl
    {
        public ConvenientTextSettingsControl()
        {
            InitializeComponent();
        }
    }
}