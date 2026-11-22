using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using SnappyLayout.Models;

namespace SnappyLayout.Services
{
    /// <summary>Tüm ayarları %AppData%\Snappy Layouts\settings.json dosyasında saklar.</summary>
    public static class Storage
    {
        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Snappy Layouts");
        public static string LegacyFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Snappy Layout");
        public static string FilePath => Path.Combine(Folder, "settings.json");

        public static AppData Data { get; private set; }
        public static event Action Changed;

        private static DispatcherTimer _saveTimer;

        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IgnoreReadOnlyProperties = true
        };

        public static void Load()
        {
            try
            {
                // Eski "Snappy Layout" klasöründen otomatik ayar geçişi ve eski klasörün temizlenmesi
                if (Directory.Exists(LegacyFolder))
                {
                    string legacyPath = Path.Combine(LegacyFolder, "settings.json");
                    if (!File.Exists(FilePath) && File.Exists(legacyPath))
                    {
                        Directory.CreateDirectory(Folder);
                        File.Copy(legacyPath, FilePath, false);
                    }
                    try { Directory.Delete(LegacyFolder, recursive: true); } catch { }
                }
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    Data = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
                }
            }
            catch (Exception ex)
            {
                try { File.Copy(FilePath, FilePath + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { }
                Logger.Log("Settings load failed: " + ex.Message);
            }
            if (Data == null) Data = new AppData();
            int prevSchema = Data.SchemaVersion;
            Data.EnsureDefaults();
            if (prevSchema != Data.SchemaVersion) SaveNow();
        }

        /// <summary>Değişiklikleri kısa bir gecikmeyle (toplu) kaydeder.</summary>
        public static void Save()
        {
            Changed?.Invoke();
            if (_saveTimer == null)
            {
                _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); SaveNow(); };
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        public static void SaveNow()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Data, JsonOptions));
                File.Move(tmp, FilePath, true);
            }
            catch (Exception ex) { Logger.Log("Save failed: " + ex.Message); }
        }

        public static void Export(string path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Data, JsonOptions));
        }

        public static void Import(string path)
        {
            var d = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path), JsonOptions);
            if (d == null) throw new InvalidDataException();
            d.EnsureDefaults();
            Data = d;
            SaveNow();
            Changed?.Invoke();
        }
    }

    public static class Logger
    {
        private static readonly object _lock = new object();
        public static void Log(string msg)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Storage.Folder);
                    var p = Path.Combine(Storage.Folder, "log.txt");
                    var fi = new FileInfo(p);
                    if (fi.Exists && fi.Length > 1_000_000) fi.Delete();
                    File.AppendAllText(p, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }
}
