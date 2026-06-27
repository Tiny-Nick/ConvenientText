using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ConvenientText.Models
{
    public class TextDataModel : ObservableObject
    {
        private string _displayText = "点击✎编辑文字";
        private Avalonia.Media.Color _textColor = Avalonia.Media.Colors.White;
        private double _fontSize = 18;
        private double _floatingX = 20;
        private double _floatingY = 100;
        private bool _isFloatingButtonEnabled = true;
        private bool _enableTimeRange = false;
        private TimeSpan _startTime = new TimeSpan(8, 0, 0);
        private TimeSpan _endTime = new TimeSpan(22, 0, 0);
        private bool _enableUsbNotification = true;

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

        public bool IsInTimeRange()
        {
            if (!EnableTimeRange) return true;
            var now = DateTime.Now.TimeOfDay;
            if (StartTime <= EndTime)
                return now >= StartTime && now <= EndTime;
            else
                return now >= StartTime || now <= EndTime;
        }
    }
}