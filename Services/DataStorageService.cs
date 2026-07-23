// ============================================================
//  DataStorageService.cs
//  作用：插件的“数据中心”。
//  负责把各个组件的设置（文字、颜色、字号、时间段、预设等）
//  以 JSON 形式读写到一个共享文件 data.json 里。
//
//  为什么需要它：ClassIsland 自己会保存每个组件的设置（Settings），
//  但悬浮按钮、设置页这些“组件之外”的窗口也需要读写同一份数据，
//  所以插件用 data.json 作为各窗口之间共享数据的桥梁。
//
//  数据流向约定（很重要）：
//    1. 任何窗口改完数据 → 调 SaveAll() 写入文件；
//    2. SaveAll() 会广播 DataChanged 事件；
//    3. 其它窗口/组件收到广播后重新 LoadAll() 读取并刷新自己。
//    注意：事件处理里【只读不写】，否则会形成无限循环。
//
//  文件位置：%AppData%\ClassIsland\Plugins\ConvenientText\data.json
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ConvenientText.Models;

namespace ConvenientText.Services
{
    /// <summary>
    /// 共享数据存储服务（在 Plugin.cs 里注册为单例）。
    /// 数据是一个字典：键 = 组件的 ComponentId，值 = 组件的设置模型。
    /// </summary>
    public class DataStorageService
    {
        /// <summary>data.json 的完整路径</summary>
        private readonly string _filePath;

        /// <summary>JSON 序列化配置（缩进格式 + 颜色转换器）</summary>
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// 数据保存后触发，用于通知各窗口/组件刷新。
        /// 只读操作（LoadAll）不会触发，只有 SaveAll 会触发。
        /// 事件处理里只允许读取，不允许再保存，避免循环。
        /// </summary>
        public event EventHandler? DataChanged;

        /// <summary>
        /// 每个组件标识圆点的候选颜色（红/蓝/绿/橙/紫）。
        /// 新组件会依次挑一个没被占用的颜色。
        /// </summary>
        private static readonly Avalonia.Media.Color[] PredefinedColors = new[]
        {
            Avalonia.Media.Color.FromRgb(0xE7, 0x4C, 0x3C), // 红
            Avalonia.Media.Color.FromRgb(0x34, 0x98, 0xDB), // 蓝
            Avalonia.Media.Color.FromRgb(0x2E, 0xCC, 0x71), // 绿
            Avalonia.Media.Color.FromRgb(0xF3, 0x9C, 0x12), // 橙
            Avalonia.Media.Color.FromRgb(0x9B, 0x59, 0xB6), // 紫
        };

        /// <summary>最多允许添加的组件数量</summary>
        public const int MAX_COMPONENTS = 5;

        public DataStorageService()
        {
            // 拼出数据文件路径：%AppData%\ClassIsland\Plugins\ConvenientText\data.json
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var pluginDir = Path.Combine(appData, "ClassIsland", "Plugins", "ConvenientText");
            Directory.CreateDirectory(pluginDir); // 目录不存在就创建（已存在则什么都不做）
            _filePath = Path.Combine(pluginDir, "data.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,                              // 缩进格式，方便人眼查看
                Converters = { new ColorJsonConverter() }          // 颜色存成 "#AARRGGBB" 字符串
            };
        }

        // ============================================================
        //  读取
        // ============================================================

        /// <summary>
        /// 读取全部组件数据。
        /// 各种异常情况（文件不存在/文件损坏/旧版格式）都会兜底，
        /// 保证返回一个可用的字典，绝不抛异常给调用方。
        /// </summary>
        public Dictionary<string, TextDataModel> LoadAll()
        {
            if (!File.Exists(_filePath))
                return CreateDefaultData(); // 第一次使用：造一份默认数据

            try
            {
                var json = File.ReadAllText(_filePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 兼容 1.0.x 旧版格式：旧版整个文件就是单个组件对象
                // （顶层直接有 DisplayText 字段），需要迁移成新的字典格式
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("DisplayText", out _))
                {
                    return MigrateFromOldFormat(json);
                }

                var dict = JsonSerializer.Deserialize<Dictionary<string, TextDataModel>>(json, _jsonOptions);
                if (dict == null || dict.Count == 0)
                    return CreateDefaultData();

                // 读出来以后顺手做一次“体检”：清理脏数据、重排序号、限量
                return RebuildIndexes(dict);
            }
            catch
            {
                // 文件损坏等任何意外：丢掉坏数据，重建默认数据
                return CreateDefaultData();
            }
        }

        // ============================================================
        //  保存
        // ============================================================

        /// <summary>
        /// 把整份数据写入文件，然后广播 DataChanged。
        /// 这是所有窗口保存数据的【唯一入口】。
        /// </summary>
        public void SaveAll(Dictionary<string, TextDataModel> dataDict)
        {
            try
            {
                var json = JsonSerializer.Serialize(dataDict, _jsonOptions);
                File.WriteAllText(_filePath, json);
            }
            catch { } // 写盘失败（权限/磁盘满等）不致命，静默跳过

            // 广播“数据变了”，让组件/悬浮按钮/设置页保持同步
            try { DataChanged?.Invoke(this, EventArgs.Empty); } catch { }
        }

        // ============================================================
        //  工具方法
        // ============================================================

        /// <summary>
        /// 为新组件挑一个没被占用的圆点颜色。
        /// </summary>
        public Avalonia.Media.Color GetNextColor(Dictionary<string, TextDataModel> existingData)
        {
            if (existingData == null || existingData.Count == 0)
                return PredefinedColors[0];

            // 收集已被有效组件占用的颜色
            var usedColors = existingData.Values
                .Where(m => m.IsValid)
                .Select(m => m.DotColor)
                .ToHashSet();

            // 返回第一个没被占用的候选色
            foreach (var color in PredefinedColors)
            {
                if (!usedColors.Contains(color))
                    return color;
            }

            return PredefinedColors[0]; // 全占用了就用红色兜底
        }

        /// <summary>
        /// 从字典里取出所有有效组件，按序号排序。
        /// </summary>
        public List<TextDataModel> GetValidComponents(Dictionary<string, TextDataModel> dataDict)
        {
            return dataDict.Values
                .Where(m => m.IsValid)
                .OrderBy(m => m.OrderIndex)
                .ToList();
        }

        // ============================================================
        //  私有方法
        // ============================================================

        /// <summary>
        /// 第一次使用时创建默认数据：一个“组件 #1”。
        /// </summary>
        private Dictionary<string, TextDataModel> CreateDefaultData()
        {
            var defaultModel = TextDataModel.CreateNew(1, PredefinedColors[0]);
            var result = new Dictionary<string, TextDataModel>
            {
                [defaultModel.ComponentId] = defaultModel
            };
            SaveAll(result); // 立刻落盘，下次启动就有文件可读
            return result;
        }

        /// <summary>
        /// 把 1.0.x 旧版的单组件格式迁移成新的字典格式。
        /// </summary>
        private Dictionary<string, TextDataModel> MigrateFromOldFormat(string oldJson)
        {
            try
            {
                var oldModel = JsonSerializer.Deserialize<TextDataModel>(oldJson, _jsonOptions);
                if (oldModel == null)
                    return CreateDefaultData();

                // 旧数据没有 ComponentId 概念，给它分配一个新的身份
                var newId = Guid.NewGuid().ToString();
                oldModel.ComponentId = newId;
                oldModel.OrderIndex = 1;
                oldModel.IsValid = true;
                oldModel.DotColor = PredefinedColors[0];

                var result = new Dictionary<string, TextDataModel>
                {
                    [newId] = oldModel
                };

                SaveAll(result); // 迁移完立刻用新格式落盘
                return result;
            }
            catch
            {
                return CreateDefaultData();
            }
        }

        /// <summary>
        /// 读取数据后的“体检”：
        /// 1. 清掉旧版本因 ComponentId 为空字符串产生的脏数据；
        /// 2. 有效组件数量截断到上限（5 个）；
        /// 3. 序号重新整理成连续的 1..N；
        /// 4. 有名额时把历史无效数据“复活”补上。
        /// </summary>
        private Dictionary<string, TextDataModel> RebuildIndexes(Dictionary<string, TextDataModel> dict)
        {
            // 清掉旧版本因为 ComponentId 为空字符串产生的脏数据
            if (dict.ContainsKey(""))
                dict.Remove("");

            var valid = dict.Values.Where(m => m.IsValid).OrderBy(m => m.OrderIndex).ToList();
            var invalid = dict.Values.Where(m => !m.IsValid).OrderBy(m => m.OrderIndex).ToList();

            int index = 1;
            var newDict = new Dictionary<string, TextDataModel>();

            // 有效组件：最多保留 MAX_COMPONENTS 个，序号重排为 1..N
            foreach (var model in valid.Take(MAX_COMPONENTS))
            {
                model.OrderIndex = index++;
                model.IsValid = true;
                newDict[model.ComponentId] = model;
            }

            // 名额没满时，把历史上被标记无效的数据复活补上
            var extraInvalid = invalid.ToList();
            while (newDict.Count < MAX_COMPONENTS && extraInvalid.Count > 0)
            {
                var model = extraInvalid.First();
                extraInvalid.RemoveAt(0);
                model.OrderIndex = index++;
                model.IsValid = true;
                model.DotColor = GetNextColor(newDict);
                newDict[model.ComponentId] = model;
            }

            // 剩下的无效数据继续保留在文件里（标记为无效）
            foreach (var model in extraInvalid)
            {
                model.OrderIndex = index++;
                model.IsValid = false;
                newDict[model.ComponentId] = model;
            }

            // 一个都不剩时，造一个默认组件兜底
            if (newDict.Count == 0)
            {
                var defaultModel = TextDataModel.CreateNew(1, PredefinedColors[0]);
                newDict[defaultModel.ComponentId] = defaultModel;
            }

            return newDict;
        }
    }

    // ============================================================
    //  颜色的 JSON 转换器
    //  Avalonia 的 Color 结构不能直接被 System.Text.Json 序列化，
    //  这里约定：文件里存成 "#AARRGGBB" 形式的字符串。
    // ============================================================
    public class ColorJsonConverter : JsonConverter<Avalonia.Media.Color>
    {
        /// <summary>读：把 "#RRGGBB" 或 "#AARRGGBB" 字符串解析成颜色</summary>
        public override Avalonia.Media.Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
                return Avalonia.Media.Colors.White;

            if (value.StartsWith("#"))
            {
                value = value.TrimStart('#');
                if (value.Length == 6) // #RRGGBB（不带透明度）
                {
                    var r = Convert.ToByte(value.Substring(0, 2), 16);
                    var g = Convert.ToByte(value.Substring(2, 2), 16);
                    var b = Convert.ToByte(value.Substring(4, 2), 16);
                    return Avalonia.Media.Color.FromRgb(r, g, b);
                }
                if (value.Length == 8) // #AARRGGBB（带透明度）
                {
                    var a = Convert.ToByte(value.Substring(0, 2), 16);
                    var r = Convert.ToByte(value.Substring(2, 2), 16);
                    var g = Convert.ToByte(value.Substring(4, 2), 16);
                    var b = Convert.ToByte(value.Substring(6, 2), 16);
                    return Avalonia.Media.Color.FromArgb(a, r, g, b);
                }
            }
            return Avalonia.Media.Colors.White; // 解析失败兜底白色
        }

        /// <summary>写：把颜色输出成 "#AARRGGBB" 字符串</summary>
        public override void Write(Utf8JsonWriter writer, Avalonia.Media.Color value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");
        }
    }
}
