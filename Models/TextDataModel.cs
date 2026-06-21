using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;

namespace ConvenientText.Models;

public class TextDataModel : ObservableObject
{
    private string _displayText = "点击✎编辑文字";
    private Color _textColor = Colors.White;  // 默认白色，跟随软件浅色/深色？我们默认白色，但你可以改
    private double _fontSize = 18;

    public string DisplayText
    {
        get => _displayText;
        set => SetProperty(ref _displayText, value);
    }

    public Color TextColor
    {
        get => _textColor;
        set => SetProperty(ref _textColor, value);
    }

    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }
}