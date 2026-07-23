// ============================================================
//  ComponentSettingsPanel.axaml.cs
//  作用：设置页里“组件详细设置”卡片的内容面板。
//  本身几乎全是 XAML 绑定（双向绑定到 TextDataModel），
//  这里只负责接收要编辑的模型并设为 DataContext。
// ============================================================

using Avalonia.Controls;
using ConvenientText.Models;

using UserControl = Avalonia.Controls.UserControl;

namespace ConvenientText.Views
{
    public partial class ComponentSettingsPanel : UserControl
    {
        public ComponentSettingsPanel()
        {
            InitializeComponent();

            // 【修复】先给一个默认数据上下文，避免还没选中组件时
            // 绑定落到外层设置窗口上，刷一堆绑定警告
            this.DataContext = new TextDataModel();
        }

        public void SetDataModel(TextDataModel model)
        {
            this.DataContext = model;
        }
    }
}
