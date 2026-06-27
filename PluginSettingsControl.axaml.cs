using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ConvenientText.Models;
using ConvenientText.Services;
using System;

namespace ConvenientText
{
    [SettingsPageInfo(
        "ConvenientTextSettings",
        "便捷文本",
        SettingsPageCategory.External
    )]
    public partial class PluginSettingsControl : SettingsPageBase
    {
        private TextDataModel? _dataModel;
        private DataStorageService? _storage;

        public PluginSettingsControl()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            _dataModel = Plugin.DataModel;
            _storage = Plugin.Storage;
            if (_dataModel == null || _storage == null) return;

            this.DataContext = _dataModel;

            _dataModel.PropertyChanged += (_, _) =>
            {
                _storage.Save(_dataModel);
            };
        }
    }
}