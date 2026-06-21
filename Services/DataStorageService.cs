using System;
using System.IO;
using System.Text.Json;
using ConvenientText.Models;

namespace ConvenientText.Services;

public class DataStorageService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public DataStorageService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pluginDir = Path.Combine(appData, "ClassIsland", "Plugins", "ConvenientText");
        Directory.CreateDirectory(pluginDir);
        _filePath = Path.Combine(pluginDir, "data.json");
    }

    public TextDataModel Load()
    {
        if (!File.Exists(_filePath))
            return new TextDataModel();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<TextDataModel>(json) ?? new TextDataModel();
        }
        catch
        {
            return new TextDataModel();
        }
    }

    public void Save(TextDataModel data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }
}