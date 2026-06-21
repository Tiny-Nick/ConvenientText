using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using ConvenientText.Models;

namespace ConvenientText.Components;

[ComponentInfo(
    "9E7F8A2D-4C1B-4E5F-9A3C-7D8B2E1F0A3C",
    "便捷文本",
    "\uE9B0",
    "可通过悬浮窗快速修改文字")]
public partial class ConvenientTextComponent : ComponentBase<TextDataModel>
{
    private readonly TextBlock _textBlock;
    private TextDataModel? _dataModel;

    public ConvenientTextComponent()
    {
        // 纯代码创建 TextBlock
        _textBlock = new TextBlock
        {
            Text = "点击✎编辑文字",
            Foreground = Avalonia.Media.Brushes.White,
            FontSize = 18,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap
        };
        Content = _textBlock;

        // 组件加载到界面时触发
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 获取共享数据模型
        _dataModel = IAppHost.GetService<TextDataModel>();
        if (_dataModel == null) return;

        // 立即更新显示
        UpdateUI();

        // 监听数据变化（悬浮窗修改时自动刷新）
        _dataModel.PropertyChanged += (_, _) =>
        {
            Dispatcher.UIThread.Post(UpdateUI);
        };
    }

    private void UpdateUI()
    {
        if (_textBlock == null || _dataModel == null) return;

        _textBlock.Text = _dataModel.DisplayText;
        _textBlock.Foreground = new Avalonia.Media.SolidColorBrush(_dataModel.TextColor);
        _textBlock.FontSize = _dataModel.FontSize;
    }
}