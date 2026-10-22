using System;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;

namespace PvPChronicle
{
    [Serializable]
    public sealed class PluginSettings
    {
        public bool OverlayEnabled { get; set; } = true;
        public bool Locked { get; set; }
        public bool Preview { get; set; }
        public int Left { get; set; } = 420;
        public int Top { get; set; } = 300;
        public int Width { get; set; } = 680;
        public int Height { get; set; } = 420;
        public int AssistWindowSeconds { get; set; } = 10;
        public bool RecordAssists { get; set; } = true;
        public int MaximumLines { get; set; } = 6;
        public int OpacityPercent { get; set; } = 96;
        public int FontSize { get; set; } = 16;
        public string Language { get; set; }
        public bool CheckUpdatesOnStartup { get; set; } = true;
        public string SkippedVersion { get; set; } = string.Empty;
        public string LastUpdateCheckUtc { get; set; } = string.Empty;

        public void Normalize()
        {
            Width = Math.Max(360, Math.Min(1400, Width)); Height = Math.Max(160, Math.Min(900, Height));
            AssistWindowSeconds = Math.Max(1, Math.Min(60, AssistWindowSeconds));
            MaximumLines = Math.Max(1, Math.Min(30, MaximumLines)); OpacityPercent = Math.Max(30, Math.Min(100, OpacityPercent));
            FontSize = Math.Max(12, Math.Min(40, FontSize));
            if (!string.IsNullOrWhiteSpace(Language)) Language = Localization.NormalizeLanguage(Language);
            if (SkippedVersion == null) SkippedVersion = string.Empty;
            if (LastUpdateCheckUtc == null) LastUpdateCheckUtc = string.Empty;
        }
        public bool InitializeLanguageIfMissing(CultureInfo culture)
        {
            if (!string.IsNullOrWhiteSpace(Language)) { Language = Localization.NormalizeLanguage(Language); return false; }
            Language = Localization.MapCulture(culture); return true;
        }
        public static PluginSettings Load(string path)
        {
            try { if (!File.Exists(path)) return new PluginSettings(); using (var s = File.OpenRead(path)) { var v = (PluginSettings)new XmlSerializer(typeof(PluginSettings)).Deserialize(s); v.Normalize(); return v; } }
            catch { return new PluginSettings(); }
        }
        public void Save(string path)
        {
            Normalize();
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var s = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new XmlSerializer(typeof(PluginSettings)).Serialize(s, this);
                    s.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
