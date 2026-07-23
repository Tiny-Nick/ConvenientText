// ============================================================
//  TextDataModel.cs
//  作用：一个“便捷文本”组件的全部设置数据。
//  包括：身份标识（ComponentId/序号/是否有效）、显示内容（文字/
//  颜色/字号）、圆点颜色、悬浮按钮位置与开关、时间段控制、
//  U盘提醒开关、预设文本库。
//
//  它继承 ObservableObject（CommunityToolkit.Mvvm），所有属性
//  变化都会触发 PropertyChanged 事件，界面绑定和“自动保存”
//  都靠这个事件驱动。
// ============================================================

using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ConvenientText.Models
{
    /// <summary>
    /// 便捷文本数据模型 - 支持多组件实例
    /// </summary>
    public partial class TextDataModel : ObservableObject
    {
        // ===== 实例标识 =====
        private string _componentId = string.Empty;
        private int _orderIndex = 0;
        private bool _isValid = true;

        // ===== 显示内容 =====
        private string _displayText = "点击✎编辑文字";
        private Avalonia.Media.Color _textColor = Avalonia.Media.Colors.White;
        private double _fontSize = 18;

        // ===== 颜色圆点 =====
        private Avalonia.Media.Color _dotColor = Avalonia.Media.Colors.Gray;

        // ===== 悬浮按钮位置 =====
        private double _floatingX = 20;
        private double _floatingY = 100;

        // ===== 悬浮按钮开关 =====
        private bool _isFloatingButtonEnabled = true;

        // ===== 时间段控制 =====
        private bool _enableTimeRange = false;
        private TimeSpan _startTime = new TimeSpan(8, 0, 0);
        private TimeSpan _endTime = new TimeSpan(22, 0, 0);

        // ===== U盘通知 =====
        private bool _enableUsbNotification = true;

        // ===== 预设列表 =====
        private ObservableCollection<string> _presets = new();

        // ============================================================
        //  属性
        // ============================================================

        public string ComponentId
        {
            get => _componentId;
            set => SetProperty(ref _componentId, value);
        }

        public int OrderIndex
        {
            get => _orderIndex;
            set => SetProperty(ref _orderIndex, value);
        }

        public bool IsValid
        {
            get => _isValid;
            set => SetProperty(ref _isValid, value);
        }

        public string DisplayText
        {
            get => _displayText;
            set => SetProperty(ref _displayText, value);
        }

        public Avalonia.Media.Color TextColor
        {
            get => _textColor;
            set => SetProperty(ref _textColor, value);
        }

        public double FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public Avalonia.Media.Color DotColor
        {
            get => _dotColor;
            set => SetProperty(ref _dotColor, value);
        }

        public double FloatingX
        {
            get => _floatingX;
            set => SetProperty(ref _floatingX, value);
        }

        public double FloatingY
        {
            get => _floatingY;
            set => SetProperty(ref _floatingY, value);
        }

        public bool IsFloatingButtonEnabled
        {
            get => _isFloatingButtonEnabled;
            set => SetProperty(ref _isFloatingButtonEnabled, value);
        }

        public bool EnableTimeRange
        {
            get => _enableTimeRange;
            set => SetProperty(ref _enableTimeRange, value);
        }

        public TimeSpan StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        public TimeSpan EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        public bool EnableUsbNotification
        {
            get => _enableUsbNotification;
            set => SetProperty(ref _enableUsbNotification, value);
        }

        public ObservableCollection<string> Presets
        {
            get => _presets;
            set => SetProperty(ref _presets, value);
        }

        // ============================================================
        //  方法
        // ============================================================

        /// <summary>
        /// 判断当前时间是否处于本组件设定的时间段内
        /// </summary>
        /// <param name="currentTime">外部传入的当前时间（来自 TimeRangeService）</param>
        public bool IsInTimeRange(TimeSpan currentTime)
        {
            if (!EnableTimeRange) return true;
            if (!IsValid) return false;

            if (StartTime <= EndTime)
                return currentTime >= StartTime && currentTime <= EndTime;
            else
                return currentTime >= StartTime || currentTime <= EndTime;
        }

        /// <summary>
        /// 【新增】把另一个模型的内容复制到本模型（用于跨窗口同步数据）。
        /// 不会复制 ComponentId，避免把身份搞乱。
        /// </summary>
        public void CopyFrom(TextDataModel other)
        {
            if (other == null) return;
            OrderIndex = other.OrderIndex;
            IsValid = other.IsValid;
            DisplayText = other.DisplayText;
            TextColor = other.TextColor;
            FontSize = other.FontSize;
            DotColor = other.DotColor;
            FloatingX = other.FloatingX;
            FloatingY = other.FloatingY;
            IsFloatingButtonEnabled = other.IsFloatingButtonEnabled;
            EnableTimeRange = other.EnableTimeRange;
            StartTime = other.StartTime;
            EndTime = other.EndTime;
            EnableUsbNotification = other.EnableUsbNotification;
            // 【修复】内容相同就不替换集合。否则每次同步都会触发 Presets 变更事件，
            // 在设置页开着详情面板时会形成“保存→同步→又保存”的无限循环（崩溃）。
            if (!Presets.SequenceEqual(other.Presets))
                Presets = new ObservableCollection<string>(other.Presets);
        }

        /// <summary>
        /// 比较两个模型的内容是否一致（用于跳过重复的保存）。
        /// </summary>
        public static bool ContentEquals(TextDataModel a, TextDataModel b)
        {
            return a.OrderIndex == b.OrderIndex &&
                   a.IsValid == b.IsValid &&
                   a.DisplayText == b.DisplayText &&
                   a.TextColor == b.TextColor &&
                   a.FontSize == b.FontSize &&
                   a.DotColor == b.DotColor &&
                   a.FloatingX == b.FloatingX &&
                   a.FloatingY == b.FloatingY &&
                   a.IsFloatingButtonEnabled == b.IsFloatingButtonEnabled &&
                   a.EnableTimeRange == b.EnableTimeRange &&
                   a.StartTime == b.StartTime &&
                   a.EndTime == b.EndTime &&
                   a.EnableUsbNotification == b.EnableUsbNotification &&
                   a.Presets.SequenceEqual(b.Presets);
        }

        public static TextDataModel CreateNew(int orderIndex, Avalonia.Media.Color dotColor)
        {
            return new TextDataModel
            {
                ComponentId = Guid.NewGuid().ToString(),
                OrderIndex = orderIndex,
                IsValid = true,
                DotColor = dotColor,
                DisplayText = $"组件 #{orderIndex}",
                TextColor = Avalonia.Media.Colors.White,
                FontSize = 18,
                IsFloatingButtonEnabled = true,
                EnableTimeRange = false,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(22, 0, 0),
                EnableUsbNotification = true
            };
        }

        public static TextDataModel CreateInvalid(int orderIndex)
        {
            return new TextDataModel
            {
                ComponentId = Guid.NewGuid().ToString(),
                OrderIndex = orderIndex,
                IsValid = false,
                DotColor = Avalonia.Media.Colors.Gray,
                DisplayText = "哎呀，最多添加5个文本组件",
                TextColor = Avalonia.Media.Colors.Gray,
                FontSize = 16,
                IsFloatingButtonEnabled = false,
                EnableTimeRange = false,
                EnableUsbNotification = false
            };
        }

        public TextDataModel Clone()
        {
            return new TextDataModel
            {
                ComponentId = this.ComponentId,
                OrderIndex = this.OrderIndex,
                IsValid = this.IsValid,
                DisplayText = this.DisplayText,
                TextColor = this.TextColor,
                FontSize = this.FontSize,
                DotColor = this.DotColor,
                FloatingX = this.FloatingX,
                FloatingY = this.FloatingY,
                IsFloatingButtonEnabled = this.IsFloatingButtonEnabled,
                EnableTimeRange = this.EnableTimeRange,
                StartTime = this.StartTime,
                EndTime = this.EndTime,
                EnableUsbNotification = this.EnableUsbNotification,
                Presets = new ObservableCollection<string>(this.Presets)
            };
        }
    }
}
